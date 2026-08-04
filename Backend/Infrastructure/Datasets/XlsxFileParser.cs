using System.Globalization;
using Application.Interfaces;
using ClosedXML.Excel;

namespace Infrastructure.Datasets;

/// <summary>
/// Reads the first worksheet of an XLSX workbook into a header row and string
/// rows. Numbers are normalized to an invariant format so downstream type
/// inference and SQLite aggregation behave the same as CSV uploads.
/// </summary>
public class XlsxFileParser : IDatasetFileParser
{
    public bool SupportsExtension(string extension) =>
        string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);

    public (string[] headers, List<string[]> rows) Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("The workbook contains no worksheets.");

        var range = sheet.RangeUsed();
        if (range is null)
            throw new InvalidDataException("The workbook contains no data.");

        var rawRows = new List<string[]>();
        var maxColumns = 0;
        foreach (var row in range.Rows())
        {
            var values = row.Cells().Select(CellToText).ToArray();
            maxColumns = Math.Max(maxColumns, values.Length);
            rawRows.Add(values);
        }

        if (rawRows.Count == 0 || maxColumns == 0)
            throw new InvalidDataException("The workbook contains no data.");

        var rectangular = rawRows
            .Select(r => Enumerable.Range(0, maxColumns)
                .Select(i => i < r.Length ? r[i] : "")
                .ToArray())
            .ToArray();

        var headers = rectangular[0].Select(h => h.Trim()).ToArray();
        var body = rectangular
            .Skip(1)
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        return (headers, body);
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