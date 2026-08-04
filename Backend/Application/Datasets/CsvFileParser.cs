using Application.Interfaces;

namespace Application.Datasets;

public class CsvFileParser : IDatasetFileParser
{
    public bool SupportsExtension(string extension) =>
        string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);

    public (string[] headers, List<string[]> rows) Parse(Stream stream) => CsvParser.Parse(stream);
}