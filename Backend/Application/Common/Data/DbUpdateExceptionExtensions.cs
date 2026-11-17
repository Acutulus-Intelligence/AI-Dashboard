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
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is DbException { SqlState: "23505" })
                return true;
        }

        return false;
    }
}
