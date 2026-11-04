namespace Application.Settings;

/// <summary>Delivery settings for transactional email.</summary>
public class EmailSettings
{
    public const string SectionName = "Email";

    public string ResendApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "AI Dashboard";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
    public int InvitationTokenExpiryHours { get; set; } = 24;
    public int InternalInvitationTokenExpiryHours { get; set; } = 168;
    public int MaxEmailInvitesPerCompanyPerDay { get; set; } = 50;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResendApiKey);
}
