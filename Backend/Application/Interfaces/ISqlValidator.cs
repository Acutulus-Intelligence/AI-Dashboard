using Domain.Enums;

namespace Application.Interfaces;

public interface ISqlValidator
{
    bool IsSelectOnly(string sql, out string? errorMessage);

    /// <summary>
    /// Dialect-aware variant. Implementations that understand the provider can
    /// use its comment rules; the default preserves the generic behavior.
    /// </summary>
    bool IsSelectOnly(string sql, DbProvider dbProvider, out string? errorMessage)
        => IsSelectOnly(sql, out errorMessage);
}
