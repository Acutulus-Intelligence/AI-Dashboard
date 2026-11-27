namespace Application.Interfaces;

public enum AuditAction
{
    ChartGenerated,
    ChartSaved,
    ChartUpdated,
    ChartExecuted,
}

/// <summary>
/// One audit event. SQL is passed in full and redacted/hashed by the
/// implementation so sensitive literal values never reach log storage.
/// </summary>
public sealed record AuditEntry(
    AuditAction Action,
    Guid UserId,
    Guid? ConnectionId,
    Guid? DatasetId,
    Guid? ChartId,
    string? ChartType,
    string? Sql,
    int? RowCount,
    string Outcome,
    string? Detail = null,
    Guid? CompanyId = null);

public interface IAuditTrail
{
    void Record(AuditEntry entry);
}
