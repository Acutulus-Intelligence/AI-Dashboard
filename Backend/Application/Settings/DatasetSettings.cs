namespace Application.Settings;

/// <summary>Limits applied when a user uploads a CSV dataset.</summary>
public class DatasetSettings
{
    public const string SectionName = "Datasets";

    public int MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxRows { get; set; } = 100_000;
    public int MaxColumns { get; set; } = 50;
    public int PreviewRows { get; set; } = 5;

    /// <summary>
    /// Maximum uncompressed size of an uploaded XLSX archive. Guards against zip
    /// bombs before the workbook is materialized. Zero disables the check.
    /// </summary>
    public long MaxExpandedBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary>Maximum number of ZIP entries in an uploaded XLSX archive.</summary>
    public int MaxArchiveEntries { get; set; } = 4096;
}