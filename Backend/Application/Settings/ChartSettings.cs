namespace Application.Settings;

/// <summary>
/// Chart presentation and data-access policy.
/// </summary>
public class ChartSettings
{
    public const string SectionName = "Charts";

    /// <summary>
    /// When true, only users who can manage a company-shared connection may save
    /// or update charts with arbitrary SQL on it. Defaults to false so existing
    /// deployments keep working; enable it when viewers should only consume
    /// charts, not author SQL.
    /// </summary>
    public bool RequireManageForSharedConnectionSql { get; set; }
}
