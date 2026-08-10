namespace Application.Interfaces;

/// <summary>Parses an uploaded tabular file into a header row and string rows.</summary>
public interface IDatasetFileParser
{
    bool SupportsExtension(string extension);

    (string[] headers, List<string[]> rows) Parse(Stream stream);
}