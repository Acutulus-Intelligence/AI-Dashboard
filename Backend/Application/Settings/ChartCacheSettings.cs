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

    /// <summary>Fallback TTL for entries that do not specify one explicitly.</summary>
    public int EntryTtlDays { get; set; } = 1;

    public int ConnectionEntryTtlMinutes { get; set; } = 5;

    /// <summary>
    /// TTL for chart results built from uploaded datasets. Zero disables Redis
    /// caching for datasets, so row data is not retained longer than necessary.
    /// </summary>
    public int DatasetEntryTtlMinutes { get; set; } = 1440;

    /// <summary>
    /// Hard read cutoff for any cache entry. Entries older than this are ignored
    /// and deleted even if their Redis TTL is longer (e.g. entries written before
    /// a TTL policy change). Zero disables the cutoff.
    /// </summary>
    public int MaxEntryAgeMinutes { get; set; } = 1440;

    public long MaxPayloadBytes { get; set; } = 5 * 1024 * 1024;
    public long L1SizeLimitBytes { get; set; } = 64 * 1024 * 1024;
    public bool EncryptPayloads { get; set; } = true;
}
