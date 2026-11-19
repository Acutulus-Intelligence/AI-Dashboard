namespace Application.Settings;

public class ChartCacheSettings
{
    public const string SectionName = "ChartCache";
    public const string L1CacheKey = "chart-cache";

    public bool Enabled { get; set; } = true;
    public bool RedisEnabled { get; set; } = true;
    public string RedisConnectionString { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "ai-dashboard:";
    public int L1TtlSeconds { get; set; } = 60;
    public int EntryTtlDays { get; set; } = 30;
    public int ConnectionEntryTtlMinutes { get; set; } = 5;
    public long MaxPayloadBytes { get; set; } = 5 * 1024 * 1024;
    public long L1SizeLimitBytes { get; set; } = 64 * 1024 * 1024;
    public bool EncryptPayloads { get; set; } = true;
}
