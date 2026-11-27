using Application.Interfaces;
using Application.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Audit;

/// <summary>
/// Structured audit sink. Writes to a dedicated "Audit" logger category so a log
/// pipeline can route it separately from application logs. Phase 1 stores events
/// in logs; a persistent table can be added behind the same interface later.
/// </summary>
public sealed class AuditTrail : IAuditTrail
{
    public const string CategoryName = "Audit";

    private readonly ILogger _logger;

    public AuditTrail(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(CategoryName);
    }

    public void Record(AuditEntry entry)
    {
        var level = string.Equals(entry.Detail, "cache", StringComparison.OrdinalIgnoreCase)
            ? LogLevel.Debug
            : LogLevel.Information;

        try
        {
            _logger.Log(
                level,
                "Audit {Action} UserId={UserId} CompanyId={CompanyId} ConnectionId={ConnectionId} " +
                "DatasetId={DatasetId} ChartId={ChartId} ChartType={ChartType} SqlHash={SqlHash} " +
                "SqlPreview={SqlPreview} RowCount={RowCount} Outcome={Outcome} Detail={Detail}",
                entry.Action,
                entry.UserId,
                entry.CompanyId,
                entry.ConnectionId,
                entry.DatasetId,
                entry.ChartId,
                entry.ChartType,
                SqlRedactor.Hash(entry.Sql),
                SqlRedactor.Redact(entry.Sql),
                entry.RowCount,
                entry.Outcome,
                entry.Detail);
        }
        catch (Exception)
        {
            // Auditing must never take down a chart request. A log pipeline that
            // drops the event is preferable to a failed generation or execution.
        }
    }
}
