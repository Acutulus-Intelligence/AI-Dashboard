namespace Infrastructure.Ai;

public class AiSettings
{
    public string Provider { get; set; } = "openrouter";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "openrouter/owl-alpha";
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.2;
}
