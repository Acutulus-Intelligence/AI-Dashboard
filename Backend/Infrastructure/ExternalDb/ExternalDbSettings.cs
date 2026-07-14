namespace Infrastructure.ExternalDb;

public class ExternalDbSettings
{
    public int PreviewMaxRows { get; set; } = 10;
    public int QueryMaxRows { get; set; } = 10_000;
    public long QueryMaxBytes { get; set; } = 10 * 1024 * 1024;
    public int QueryTimeoutSeconds { get; set; } = 30;
    public string[] BlockedHosts { get; set; } =
    [
        "169.254.169.254",
        "metadata.google.internal"
    ];
}
