namespace Application.Settings;

/// <summary>Limits applied when a user uploads a CSV dataset.</summary>
public class DatasetSettings
{
    public const string SectionName = "Datasets";

    public int MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxRows { get; set; } = 100_000;
    public int MaxColumns { get; set; } = 50;
    public int PreviewRows { get; set; } = 5;
}