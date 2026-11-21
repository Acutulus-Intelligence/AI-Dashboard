using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Data;

public static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// True when the provider rejected the update with PostgreSQL's unique
    /// violation (SQLSTATE 23505). Other database failures must not be turned
    /// into a 409 conflict.
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException exception)
        => exception.HasSqlState("23505");

    /// <summary>
    /// True when the provider rejected the update with PostgreSQL's foreign-key
    /// violation (SQLSTATE 23503), for example because a referenced row was
    /// deleted after it was validated.
    /// </summary>
    public static bool IsForeignKeyViolation(this DbUpdateException exception)
        => exception.HasSqlState("23503");

    private static bool HasSqlState(this DbUpdateException exception, string sqlState)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is DbException { SqlState: var state } && state == sqlState)
                return true;
        }

        return false;
    }
}
