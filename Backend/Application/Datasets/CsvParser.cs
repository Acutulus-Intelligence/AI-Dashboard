using System.Text;

namespace Application.Datasets;

/// <summary>
/// Minimal RFC-4180-ish CSV reader. Handles quoted fields, commas and newlines
/// inside quotes, CRLF/LF line endings and an optional UTF-8 BOM.
/// </summary>
public static class CsvParser
{
    public static (string[] headers, List<string[]> rows) Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void FinishRecord()
        {
            fields.Add(field.ToString());
            records.Add(fields.ToArray());
            field.Clear();
            fields.Clear();
        }

        var buffer = new char[8192];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var ch = buffer[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < read && buffer[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(ch);
                    }
                    continue;
                }

                switch (ch)
                {
                    case '"' when field.Length == 0:
                        inQuotes = true;
                        break;
                    case ',':
                        fields.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\n':
                        FinishRecord();
                        break;
                    default:
                        if (ch != '\r')
                            field.Append(ch);
                        break;
                }
            }
        }

        if (field.Length > 0 || fields.Count > 0)
            FinishRecord();

        if (records.Count == 0)
            throw new InvalidDataException("The uploaded file contains no CSV data.");

        var headerRow = records[0].Select(header => header.Trim()).ToArray();
        var body = records
            .Skip(1)
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        return (headerRow, body);
    }
}