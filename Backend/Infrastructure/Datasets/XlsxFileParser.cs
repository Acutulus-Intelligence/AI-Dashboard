using System.Globalization;
using System.IO.Compression;
using Application.Interfaces;
using Application.Settings;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;

namespace Infrastructure.Datasets;

/// <summary>
/// Reads the first worksheet of an XLSX workbook into a header row and string
/// rows. Numbers are normalized to an invariant format so downstream type
/// inference and SQLite aggregation behave the same as CSV uploads.
///
/// Dimensions, expanded archive size and row/column counts are checked before the
/// used range is materialized, so a crafted workbook cannot exhaust server memory.
/// </summary>
public class XlsxFileParser : IDatasetFileParser
{
    private readonly DatasetSettings _settings;

    public XlsxFileParser(IOptions<DatasetSettings> settings)
    {
        _settings = settings.Value;
    }

    public bool SupportsExtension(string extension) =>
        string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);

    public (string[] headers, List<string[]> rows) Parse(Stream stream)
    {
        EnsureArchiveWithinLimit(stream);

        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("The workbook contains no worksheets.");

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (lastRow == 0 || lastColumn == 0)
            throw new InvalidDataException("The workbook contains no data.");

        if (lastRow - 1 > _settings.MaxRows)
            throw new ArgumentException($"Files may have at most {_settings.MaxRows} rows.");

        if (lastColumn > _settings.MaxColumns)
            throw new ArgumentException($"Files may have at most {_settings.MaxColumns} columns.");

        var rawRows = new List<string[]>(lastRow);
        for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
        {
            var values = new string[lastColumn];
            for (var columnNumber = 1; columnNumber <= lastColumn; columnNumber++)
                values[columnNumber - 1] = CellToText(sheet.Cell(rowNumber, columnNumber));

            rawRows.Add(values);
        }

        var headers = rawRows[0].Select(h => h.Trim()).ToArray();
        var body = rawRows
            .Skip(1)
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        return (headers, body);
    }

    /// <summary>
    /// Reads the ZIP entries and counts the bytes that actually decompress. A
    /// crafted archive can lie about the declared length in the central
    /// directory, so the check cannot trust <see cref="ZipArchiveEntry.Length"/>
    /// alone. Runs before ClosedXML loads the archive, which is where a zip bomb
    /// would do damage.
    /// </summary>
    private void EnsureArchiveWithinLimit(Stream stream)
    {
        if (_settings.MaxExpandedBytes <= 0 || !stream.CanSeek)
            return;

        var start = stream.Position;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var maxEntries = _settings.MaxArchiveEntries > 0
                ? _settings.MaxArchiveEntries
                : int.MaxValue;
            if (archive.Entries.Count > maxEntries)
                throw new ArgumentException("The workbook contains too many archive entries.");

            var buffer = new byte[81920];
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                using var entryStream = entry.Open();
                int read;
                while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                    if (total > _settings.MaxExpandedBytes)
                    {
                        throw new ArgumentException(
                            $"The workbook expands to more than {_settings.MaxExpandedBytes / (1024 * 1024)} MB.");
                    }
                }
            }
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException("The file is not a valid XLSX workbook.");
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            throw new ArgumentException("The file is not a readable XLSX workbook.");
        }
        finally
        {
            stream.Position = start;
        }
    }

    private static string CellToText(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank || value.IsError)
            return "";

        if (value.IsNumber)
            return value.GetNumber().ToString(CultureInfo.InvariantCulture);

        if (value.IsDateTime)
            return value.GetDateTime().ToString("s", CultureInfo.InvariantCulture);

        if (value.IsBoolean)
            return value.GetBoolean() ? "true" : "false";

        if (value.IsText)
            return value.GetText();

        return cell.GetFormattedString();
    }
}
