namespace Infrastructure.Ai;

public class AiSettings
{
    public string Provider { get; set; } = "openrouter";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "openrouter/owl-alpha";
    /// <summary>
    /// Completion budget. Reasoning models spend part of this on thinking;
    /// keep high enough that chart-type changes still leave room for JSON output.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// When true, log the extracted model JSON at Information level.
    /// </summary>
    public bool LogResponses { get; set; } = true;
}
