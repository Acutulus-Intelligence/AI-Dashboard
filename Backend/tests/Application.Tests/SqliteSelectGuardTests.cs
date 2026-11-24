using Application.Services;
using FluentAssertions;

namespace Application.Tests;

public sealed class SqliteSelectGuardTests
{
    [Theory]
    [InlineData("SELECT category, SUM(amount) AS total FROM data GROUP BY category")]
    [InlineData("WITH x AS (SELECT 1 AS n) SELECT * FROM x")]
    [InlineData("SELECT * FROM data;")]
    [InlineData("SELECT replace(name, 'a', 'b') FROM data")]
    [InlineData("SELECT * FROM data WHERE name = 'DROP TABLE data'")]
    public void Allows_safe_selects(string sql)
    {
        SqliteSelectGuard.IsSelectOnly(sql, out var error).Should().BeTrue(error);
    }

    [Theory]
    [InlineData("ATTACH DATABASE '/tmp/leak.db' AS leak")]
    [InlineData("PRAGMA database_list")]
    [InlineData("DROP TABLE data")]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("WITH x AS (DELETE FROM data) SELECT * FROM x")]
    [InlineData("SELECT load_extension('evil')")]
    [InlineData("INSERT INTO data VALUES ('x')")]
    public void Rejects_unsafe_sql(string sql)
    {
        SqliteSelectGuard.IsSelectOnly(sql, out var error).Should().BeFalse(error);
    }
}
