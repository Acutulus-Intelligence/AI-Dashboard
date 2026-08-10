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

public class CollectionService : ICollectionService
{
    private readonly IApplicationDbContext _db;
    private readonly IAiService _aiService;
    private readonly IDataQueryExecutor _dataQueryExecutor;
    private readonly ICollectionAccessService _access;
    private readonly IDatasetFileParser[] _parsers;
    private readonly DatasetSettings _settings;

    public CollectionService(
        IApplicationDbContext db,
        IAiService aiService,
        IDataQueryExecutor dataQueryExecutor,
        ICollectionAccessService access,
        IEnumerable<IDatasetFileParser> parsers,
        IOptions<DatasetSettings> settings)
    {
        _db = db;
        _aiService = aiService;
        _dataQueryExecutor = dataQueryExecutor;
        _access = access;
        _parsers = parsers.ToArray();
        _settings = settings.Value;
    }

    public async Task<CollectionResponse> CreateAsync(Guid userId, CreateCollectionRequest request, CancellationToken ct = default)
    {
        if (!await _access.HasCollectionManagePermissionAsync(userId, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage data collections.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > 200)
            throw new ArgumentException("Collection name must be between 1 and 200 characters.");

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var scope = await ResolveVisibilityAsync(user, request.Visibility, request.AllowedRoleIds, ct);

        await EnsureUniqueCollectionNameAsync(userId, user.CompanyId, name, scope.Visibility, ct: ct);

        var collection = new DataCollection
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CompanyId = user.CompanyId,
            CreatedById = userId,
            Visibility = scope.Visibility,
            AllowedRoleIds = scope.AllowedRoleIds,
            CreatedAt = DateTime.UtcNow,
        };

        _db.DataCollections.Add(collection);
        await _db.SaveChangesAsync(ct);

        return new CollectionResponse(
            collection.Id, collection.Name, collection.Description,
            collection.CompanyId, collection.CreatedById,
            collection.Visibility, collection.AllowedRoleIds,
            0, 0, collection.CreatedAt);
    }

    public async Task<CollectionResponse> UpdateAsync(
        Guid id, Guid userId, UpdateCollectionRequest request, CancellationToken ct = default)
    {
        var collection = await _db.DataCollections
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Collection not found.");

        if (!await _access.CanManageAsync(id, userId, ct))
            throw new UnauthorizedAccessException("You do not have permission to manage this collection.");

        var name = request.Name.Trim();
        if (name.Length == 0 || name.Length > 200)
            throw new ArgumentException("Collection name must be between 1 and 200 characters.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var scope = await ResolveVisibilityAsync(user, request.Visibility, request.AllowedRoleIds, ct);

        await EnsureUniqueCollectionNameAsync(userId, collection.CompanyId, name, scope.Visibility, excludeId: id, ct: ct);

        collection.Name = name;
        collection.Description = description;
        collection.CompanyId = user.CompanyId;
        collection.Visibility = scope.Visibility;
        collection.AllowedRoleIds = scope.AllowedRoleIds;

        await _db.SaveChangesAsync(ct);

        return new CollectionResponse(
            collection.Id, collection.Name, collection.Description,
            collection.CompanyId, collection.CreatedById,
            collection.Visibility, collection.AllowedRoleIds,
            0, 0, collection.CreatedAt);
    }

    public async Task<List<CollectionResponse>> GetCollectionsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var viewable = await _db.DataCollections
            .AsNoTracking()
            .Select(c => new
            {
                Collection = c,
                FileCount = c.Files.Count,
                RowCount = c.Files.Sum(f => f.RowCount)
            })
            .ToListAsync(ct);

        return viewable
            .Where(x => IsViewable(x.Collection, user))
            .OrderByDescending(x => x.Collection.CreatedAt)
            .Select(x => new CollectionResponse(
                x.Collection.Id,
                x.Collection.Name,
                x.Collection.Description,
                x.Collection.CompanyId,
                x.Collection.CreatedById,
                x.Collection.Visibility,
                x.Collection.AllowedRoleIds,
                x.FileCount,
                x.RowCount,
                x.Collection.CreatedAt))
            .ToList();
    }

    private static bool IsViewable(DataCollection collection, User user)
    {
        if (collection.CompanyId is null)
            return collection.CreatedById == user.Id;

        if (user.CompanyId != collection.CompanyId)
            return false;

        if (collection.CreatedById == user.Id)
            return true;

        if (collection.Visibility == CollectionVisibility.Company)
            return true;

        if (collection.Visibility == CollectionVisibility.Roles)
        {
            if (user.CompanyRoleId.HasValue && collection.AllowedRoleIds.Contains(user.CompanyRoleId.Value))
                return true;
            return user.CompanyRole is not null && user.CompanyRole.CanManageConnections;
        }

        return false;
    }

    public async Task<CollectionDetailResponse> GetCollectionAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var collection = await GetViewableAsync(id, userId, ct);

        var files = await _db.SavedDatasets
            .AsNoTracking()
            .Where(f => f.CollectionId == collection.Id)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new CollectionFileResponse(
                f.Id, f.Name, f.TableName, f.ColumnNames.Length, f.RowCount, f.CreatedAt))
            .ToListAsync(ct);

        return new CollectionDetailResponse(
            collection.Id,
            collection.Name,
            collection.Description,
            collection.CompanyId,
            collection.CreatedById,
            collection.Visibility,
            collection.AllowedRoleIds,
            collection.CreatedAt,
            files);
    }

    public async Task DeleteCollectionAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var collection = await GetManageableAsync(id, userId, ct);
        _db.DataCollections.Remove(collection);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CollectionFileResponse> UploadFileAsync(Guid collectionId, Guid userId, string fileName, Stream fileStream, CancellationToken ct = default)
    {
        var collection = await GetManageableAsync(collectionId, userId, ct);

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
        if (name.Length == 0) name = "file";
        if (name.Length > 200) name = name[..200];
        name = await EnsureUniqueFileNameAsync(collection.Id, name, ct);

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
            CollectionId = collection.Id,
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

        return new CollectionFileResponse(dataset.Id, dataset.Name, dataset.TableName, dataset.ColumnNames.Length, dataset.RowCount, dataset.CreatedAt);
    }

    public async Task<CollectionFileDetailResponse> GetFileAsync(Guid collectionId, Guid fileId, Guid userId, CancellationToken ct = default)
    {
        await GetViewableAsync(collectionId, userId, ct);
        var dataset = await GetFileAsync(collectionId, fileId, ct);
        var rows = DatasetRows.Decode(dataset);

        return new CollectionFileDetailResponse(
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

    public async Task DeleteFileAsync(Guid collectionId, Guid fileId, Guid userId, CancellationToken ct = default)
    {
        await GetManageableAsync(collectionId, userId, ct);
        var dataset = await GetFileAsync(collectionId, fileId, ct);
        _db.SavedDatasets.Remove(dataset);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ChartConfigResponse> GenerateChartAsync(Guid collectionId, Guid fileId, Guid userId, GenerateCollectionChartRequest request, CancellationToken ct = default)
    {
        if (request.Mode is not ("prompt" or "prefab" or "auto"))
            throw new ArgumentException("Invalid mode.");

        await GetViewableAsync(collectionId, userId, ct);
        var dataset = await GetFileAsync(collectionId, fileId, ct);

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

        var config = await _aiService.GenerateCollectionChartConfigAsync(schemaJson, prompt, request.PrefabChartType, ct);
        if (config.DataModel is null)
            throw new InvalidOperationException("AI returned no data model for this data.");

        ValidateDataModel(config.DataModel, columnNames, columnTypes);

        var result = await _dataQueryExecutor.ExecuteAsync(columnNames, columnTypes, rows, config.DataModel, ct);

        return new ChartConfigResponse(
            config.ChartType,
            config.Title,
            config.XAxis,
            config.YAxis,
            config.Aggregation,
            config.GroupBy,
            string.Empty,
            result,
            config.StyleConfig,
            config.DataModel);
    }

    private static void ValidateDataModel(DataQueryModel model, IReadOnlyList<string> columnNames, IReadOnlyList<string> columnTypes)
    {
        var validOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "eq", "neq", "gt", "gte", "lt", "lte", "contains", "in", "notin", "isnull", "isnotnull"
        };

        foreach (var filter in model.Filters)
        {
            if (!columnNames.Contains(filter.Column))
                throw new InvalidOperationException($"AI referenced unknown column '{filter.Column}'.");
            if (!validOperators.Contains(filter.Operator))
                throw new InvalidOperationException($"AI used unsupported filter operator '{filter.Operator}'.");
        }

        foreach (var group in model.GroupBy)
        {
            if (!columnNames.Contains(group))
                throw new InvalidOperationException($"AI grouped by unknown column '{group}'.");
        }

        var validFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "count", "sum", "avg", "min", "max"
        };

        foreach (var agg in model.Aggregations)
        {
            if (!columnNames.Contains(agg.Column))
                throw new InvalidOperationException($"AI aggregated unknown column '{agg.Column}'.");
            if (!validFunctions.Contains(agg.Function))
                throw new InvalidOperationException($"AI used unsupported aggregation function '{agg.Function}'.");
        }

        foreach (var order in model.OrderBy)
        {
            if (!columnNames.Contains(order.Column))
                throw new InvalidOperationException($"AI ordered by unknown column '{order.Column}'.");
            if (order.Direction is not ("asc" or "desc"))
                throw new InvalidOperationException($"AI used unsupported sort direction '{order.Direction}'.");
        }

        if (model.Limit is < 0 or > 100_000)
            throw new InvalidOperationException("AI used an out-of-range row limit.");
    }

    private async Task<VisibilityScope> ResolveVisibilityAsync(
        User user,
        CollectionVisibility requestedVisibility,
        List<Guid>? requestedRoleIds,
        CancellationToken ct)
    {
        var visibility = requestedVisibility;
        var allowedRoleIds = requestedRoleIds ?? [];

        if (user.UserType != UserType.Company || user.CompanyId is null)
            return new VisibilityScope(CollectionVisibility.Private, []);

        if (visibility == CollectionVisibility.Roles)
        {
            if (allowedRoleIds.Count == 0)
                throw new InvalidOperationException("Select at least one role to share this collection with.");

            var validIds = await _db.CompanyRoles
                .Where(r => r.CompanyId == user.CompanyId.Value)
                .Select(r => r.Id)
                .ToListAsync(ct);

            if (allowedRoleIds.Except(validIds).Any())
                throw new InvalidOperationException("One or more selected roles do not belong to your company.");
        }
        else
        {
            allowedRoleIds = [];
        }

        return new VisibilityScope(visibility, allowedRoleIds);
    }

    private sealed record VisibilityScope(CollectionVisibility Visibility, List<Guid> AllowedRoleIds);

    private async Task EnsureUniqueCollectionNameAsync(
        Guid userId, Guid? companyId, string name, CollectionVisibility visibility, Guid? excludeId = null, CancellationToken ct = default)
    {
        var candidate = name;
        var suffix = 2;
        while (true)
        {
            var conflict = companyId.HasValue && visibility != CollectionVisibility.Private
                ? await _db.DataCollections.AnyAsync(
                    c => c.CompanyId == companyId && c.Visibility != CollectionVisibility.Private
                        && c.Name == candidate && c.Id != excludeId, ct)
                : await _db.DataCollections.AnyAsync(
                    c => c.CreatedById == userId && (c.CompanyId == null || c.Visibility == CollectionVisibility.Private)
                        && c.Name == candidate && c.Id != excludeId, ct);

            if (!conflict)
                break;

            var trimmedBase = name.Length > 190 ? name[..190] : name;
            candidate = $"{trimmedBase} ({suffix})";
            suffix++;
        }
    }

    private async Task<string> EnsureUniqueFileNameAsync(Guid collectionId, string name, CancellationToken ct)
    {
        var candidate = name;
        var suffix = 2;
        while (await _db.SavedDatasets.AnyAsync(f => f.CollectionId == collectionId && f.Name == candidate, ct))
        {
            var trimmedBase = name.Length > 190 ? name[..190] : name;
            candidate = $"{trimmedBase} ({suffix})";
            suffix++;
        }
        return candidate;
    }

    private async Task<DataCollection> GetViewableAsync(Guid collectionId, Guid userId, CancellationToken ct)
    {
        return await _access.FindViewableAsync(collectionId, userId, ct)
            ?? throw new KeyNotFoundException("Collection not found.");
    }

    private async Task<DataCollection> GetManageableAsync(Guid collectionId, Guid userId, CancellationToken ct)
    {
        return await _access.FindManageableAsync(collectionId, userId, ct)
            ?? throw new UnauthorizedAccessException("You do not have permission to manage this collection.");
    }

    private async Task<SavedDataset> GetFileAsync(Guid collectionId, Guid fileId, CancellationToken ct)
    {
        return await _db.SavedDatasets
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId && f.CollectionId == collectionId, ct)
            ?? throw new KeyNotFoundException("File not found in this collection.");
    }

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