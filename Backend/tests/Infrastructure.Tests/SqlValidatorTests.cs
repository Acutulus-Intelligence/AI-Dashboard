using FluentAssertions;
using Domain.Enums;
using Infrastructure.ExternalDb.Services;

namespace Infrastructure.Tests;

public sealed class SqlValidatorTests
{
    private readonly SqlValidator _validator = new();

    [Theory]
    [InlineData("SELECT * INTO backup FROM orders")]
    [InlineData("SELECT * INTO #temp FROM orders")]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'Server=x', 'SELECT 1')")]
    [InlineData("SELECT * FROM OPENQUERY(linked, 'SELECT 1')")]
    [InlineData("SELECT * FROM orders FOR UPDATE")]
    [InlineData("SELECT * FROM orders LOCK IN SHARE MODE")]
    [InlineData("SELECT * FROM orders; DELETE FROM orders")]
    [InlineData("SELECT pg_sleep(1) FROM orders")]
    [InlineData("SELECT 1 /*! INTO OUTFILE '/tmp/x' */")]
    [InlineData("INSERT INTO orders VALUES (1)")]
    [InlineData("UPDATE orders SET amount = 0")]
    public void Rejects_unsafe_sql(string sql)
    {
        _validator.IsSelectOnly(sql, out var error).Should().BeFalse(error);
    }

    [Theory]
    [InlineData("SELECT * FROM orders")]
    [InlineData("SELECT category, SUM(amount) AS amount FROM orders GROUP BY category")]
    [InlineData("SELECT 'INTO' AS note FROM orders")]
    [InlineData("SELECT \"into\" FROM orders")]
    [InlineData("SELECT * FROM orders WHERE note = 'DROP TABLE orders'")]
    [InlineData("SELECT * FROM orders WHERE id IN (SELECT id FROM orders)")]
    [InlineData("WITH x AS (SELECT 1 AS n) SELECT * FROM x")]
    [InlineData("SELECT * FROM orders -- trailing comment")]
    [InlineData("SELECT * FROM orders WHERE note = 'pg_sleep(1)'")]
    [InlineData("SELECT $$INTO$$ AS note")]
    public void Allows_safe_sql(string sql)
    {
        _validator.IsSelectOnly(sql, out var error).Should().BeTrue(error);
    }

    [Fact]
    public void Postgres_treats_double_dash_without_space_as_comment()
    {
        _validator.IsSelectOnly(
            "SELECT * FROM orders --INTO",
            DbProvider.PostgreSql,
            out var error).Should().BeTrue(error);
    }

    [Fact]
    public void Mysql_does_not_treat_double_dash_without_space_as_comment()
    {
        _validator.IsSelectOnly(
            "SELECT 1 --x INTO OUTFILE '/tmp/x'",
            DbProvider.MySql,
            out _).Should().BeFalse();
    }

    [Fact]
    public void Mysql_treats_hash_as_comment()
    {
        _validator.IsSelectOnly(
            "SELECT * FROM orders # INTO OUTFILE '/tmp/x'",
            DbProvider.MySql,
            out var error).Should().BeTrue(error);
    }

    [Fact]
    public void Postgres_does_not_treat_hash_as_comment()
    {
        _validator.IsSelectOnly(
            "SELECT * FROM orders # INTO",
            DbProvider.PostgreSql,
            out _).Should().BeFalse();
    }
}
