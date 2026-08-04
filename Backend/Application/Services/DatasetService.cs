using System.Text.Json;
using Application.Datasets;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Application.Settings;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class DatasetService : IDatasetService
{
    private readonly IApplicationDbContext _db;
    private readonly IAiService _aiService;
    private readonly ISqlValidator _sqlValidator;
    private readonly IDatasetQueryExecutor _datasetQueryExecutor;
    private readonly IDatasetFileParser[] _parsers;
    private readonly DatasetSettings _settings;

    public DatasetService(
        IApplicationDbContext db,
        IAiService aiService,
        ISqlValidator sqlValidator,
        IDatasetQueryExecutor datasetQueryExecutor,
        IEnumerable<IDatasetFileParser> parsers,
        IOptions<DatasetSettings> settings)
    {
        _db = db;
        _aiService = aiService;
        _sqlValidator = sqlValidator;
        _datasetQueryExecutor = datasetQueryExecutor;
        _parsers = parsers.ToArray();
        _settings = settings.Value;
    }

    public async Task<DatasetResponse> UploadAsync(Guid userId, string fileName, Stream fileStream, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        var parser = _parsers.FirstOrDefault(p => p.SupportsExtension(extension))
            ?? throw new ArgumentException("Only .csv or .xlsx files are supported.");

        if (fileStream.Length > _settings.MaxFileBytes)
            throw new ArgumentException($"Files must be at most {_settings.MaxFileBytes / (1024 * 1024)} MB.");

        var (headers, rows) = parser.Parse(fileStream);
        if (headers.Length == 0)
            throw new ArgumentException("The file must have at least one column.");

        if (headers.Length > _settings.MaxColumns)
            throw new ArgumentException($"Files may have at most {_settings.MaxColumns} columns.");

        if (rows.Count > _settings.MaxRows)
            throw new ArgumentException($"Files may have at most {_settings.MaxRows} rows.");

        var normalizedColumns = NormalizeColumnNames(headers);
        var columnTypes = new string[headers.Length];
        for (var i = 0; i < headers.Length; i++)
            columnTypes[i] = DatasetTypeInferrer.Infer(rows.Select(r => i < r.Length ? r[i].Trim() : "").ToArray());

        if (normalizedColumns.Distinct().Count() != normalizedColumns.Length)
            throw new ArgumentException("Column names must be unique.");

        var name = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (name.Length == 0) name = "dataset";
        if (name.Length > 200) name = name[..200];
        name = await EnsureUniqueNameAsync(userId, name, ct);

        var rowArrays = rows.Select(r =>
        {
            var arr = new string[headers.Length];
            for (var i = 0; i < headers.Length; i++)
                arr[i] = (i < r.Length ? r[i] : "").Trim();
            return arr;
        }).ToList();

        var dataset = new SavedDataset
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            TableName = DatasetTableName.Sanitize(name),
            ColumnNames = normalizedColumns,
            ColumnTypes = columnTypes,
            RowsJson = JsonSerializer.Serialize(rowArrays),
            RowCount = rowArrays.Count,
            CreatedAt = DateTime.UtcNow,
        };

        _db.SavedDatasets.Add(dataset);
        await _db.SaveChangesAsync(ct);

        return MapResponse(dataset);
    }

    public async Task<List<DatasetResponse>> GetDatasetsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.SavedDatasets
            .AsNoTracking()
            .Where(ds => ds.UserId == userId)
            .OrderByDescending(ds => ds.CreatedAt)
            .Select(ds => new DatasetResponse(ds.Id, ds.Name, ds.ColumnNames.Length, ds.RowCount, ds.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<DatasetDetailResponse> GetDatasetAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var dataset = await GetOwnedAsync(id, userId, ct);
        var rows = DatasetRows.Decode(dataset);

        return new DatasetDetailResponse(
            dataset.Id,
            dataset.Name,
            dataset.TableName,
            dataset.ColumnNames.Select((n, i) => new DatasetColumnResponse(n, dataset.ColumnTypes[i])).ToList(),
            dataset.RowCount,
            dataset.CreatedAt,
            rows.Take(_settings.PreviewRows).Select(r =>
            {
                var dict = new Dictionary<string, object?>();
                for (var i = 0; i < dataset.ColumnNames.Length; i++)
                    dict[dataset.ColumnNames[i]] = i < r.Length && !string.IsNullOrWhiteSpace(r[i]) ? r[i] : null;
                return dict;
            }).ToList());
    }

    public async Task DeleteDatasetAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var dataset = await GetOwnedAsync(id, userId, ct);
        _db.SavedDatasets.Remove(dataset);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ChartConfigResponse> GenerateChartAsync(Guid id, Guid userId, GenerateDatasetChartRequest request, CancellationToken ct = default)
    {
        if (request.Mode is not ("prompt" or "prefab" or "auto"))
            throw new ArgumentException("Invalid mode.");

        var dataset = await GetOwnedAsync(id, userId, ct);
        var columnNames = dataset.ColumnNames;
        var columnTypes = dataset.ColumnTypes;
        var rows = DatasetRows.Decode(dataset);

        var schemaJson = JsonSerializer.Serialize(new
        {
            table = dataset.TableName,
            columns = columnNames.Select((name, i) => new { name, type = columnTypes[i], nullable = true })
        });

        var prompt = request.Mode switch
        {
            "prompt" => request.Prompt ?? "Show me this data in a chart.",
            "prefab" => $"Create a {request.PrefabChartType} chart for this table.",
            "auto" => "Choose the best visualization for this data.",
            _ => "Show me this data in a chart."
        };

        var config = await _aiService.GenerateChartConfigAsync(schemaJson, prompt, DbProvider.Sqlite, request.PrefabChartType, ct);

        if (!_sqlValidator.IsSelectOnly(config.SqlQuery, out var errorMessage))
            throw new InvalidOperationException($"AI generated an invalid query: {errorMessage}");

        var result = await _datasetQueryExecutor.ExecuteAsync(columnNames, columnTypes, rows, dataset.TableName, config.SqlQuery, ct);

        return new ChartConfigResponse(
            config.ChartType,
            config.Title,
            config.XAxis,
            config.YAxis,
            config.Aggregation,
            config.GroupBy,
            config.SqlQuery,
            result,
            config.StyleConfig);
    }

    private async Task<string> EnsureUniqueNameAsync(Guid userId, string name, CancellationToken ct)
    {
        var candidate = name;
        var suffix = 2;
        while (await _db.SavedDatasets.AnyAsync(ds => ds.UserId == userId && ds.Name == candidate, ct))
        {
            var trimmedBase = name.Length > 190 ? name[..190] : name;
            candidate = $"{trimmedBase} ({suffix})";
            suffix++;
        }
        return candidate;
    }

    private async Task<SavedDataset> GetOwnedAsync(Guid id, Guid userId, CancellationToken ct)
    {
        return await _db.SavedDatasets
            .AsNoTracking()
            .FirstOrDefaultAsync(ds => ds.Id == id && ds.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Dataset not found.");
    }

    private static DatasetResponse MapResponse(SavedDataset dataset) =>
        new(dataset.Id, dataset.Name, dataset.ColumnNames.Length, dataset.RowCount, dataset.CreatedAt);

    private static string[] NormalizeColumnNames(string[] headers)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new string[headers.Length];
        for (var i = 0; i < headers.Length; i++)
        {
            var baseName = string.IsNullOrWhiteSpace(headers[i])
                ? $"column{i + 1}"
                : headers[i];
            var candidate = baseName;
            var suffix = 2;
            while (used.Contains(candidate))
            {
                candidate = $"{baseName}_{suffix}";
                suffix++;
            }
            used.Add(candidate);
            result[i] = candidate;
        }
        return result;
    }
}