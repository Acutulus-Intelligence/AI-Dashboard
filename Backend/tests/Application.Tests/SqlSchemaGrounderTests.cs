using Application.Services;
using FluentAssertions;

namespace Application.Tests;

public class SqlSchemaGrounderTests
{
    private static readonly string[] SalesColumns = ["id", "category", "amount"];

    [Fact]
    public void Accepts_grouped_select_on_allowed_table()
    {
        var sql = """SELECT category, SUM(amount) AS amount FROM sales GROUP BY category""";

        SqlSchemaGrounder.TryValidate(sql, "sales", SalesColumns, out var error)
            .Should().BeTrue(error);
        error.Should().BeNull();
    }

    [Fact]
    public void Accepts_quoted_postgres_identifiers()
    {
        var sql = "SELECT \"category\", SUM(\"amount\") AS \"amount\" FROM \"sales\" GROUP BY \"category\"";

        SqlSchemaGrounder.TryValidate(sql, "sales", SalesColumns, out var error)
            .Should().BeTrue(error);
    }

    [Fact]
    public void Accepts_table_alias_and_qualified_columns()
    {
        var sql = """SELECT s.category, SUM(s.amount) AS amount FROM sales s GROUP BY s.category""";

        SqlSchemaGrounder.TryValidate(sql, "sales", SalesColumns, out var error)
            .Should().BeTrue(error);
    }

    [Fact]
    public void Accepts_cast_and_date_trunc()
    {
        var sql = """
            SELECT DATE_TRUNC('month', created_at) AS month, CAST(amount AS NUMERIC) AS amount
            FROM sales
            GROUP BY 1
            """;

        SqlSchemaGrounder.TryValidate(sql, "sales", ["created_at", "amount"], out var error)
            .Should().BeTrue(error);
    }

    [Fact]
    public void Accepts_select_star()
    {
        SqlSchemaGrounder.TryValidate("SELECT * FROM sales", "sales", SalesColumns, out var error)
            .Should().BeTrue(error);
    }

    [Fact]
    public void Accepts_cte_that_reads_allowed_table()
    {
        var sql = """
            WITH totals AS (
              SELECT category, SUM(amount) AS amount FROM sales GROUP BY category
            )
            SELECT category, amount FROM totals
            """;

        SqlSchemaGrounder.TryValidate(sql, "sales", SalesColumns, out var error)
            .Should().BeTrue(error);
    }

    [Fact]
    public void Rejects_invented_column()
    {
        var sql = "SELECT customer_email, SUM(amount) AS amount FROM sales GROUP BY customer_email";

        SqlSchemaGrounder.TryValidate(sql, "sales", SalesColumns, out var error)
            .Should().BeFalse();
        error.Should().Contain("customer_email");
        error.Should().Contain("Allowed columns");
    }

    [Fact]
    public void Rejects_invented_table()
    {
        var sql = "SELECT category, amount FROM orders";

        SqlSchemaGrounder.TryValidate(sql, "sales", SalesColumns, out var error)
            .Should().BeFalse();
        error.Should().Contain("orders");
        error.Should().Contain("sales");
    }

    [Fact]
    public void Rejects_join_to_unknown_table()
    {
        var sql = """
            SELECT s.category, c.email
            FROM sales s
            JOIN customers c ON c.id = s.id
            """;

        SqlSchemaGrounder.TryValidate(sql, "sales", SalesColumns, out var error)
            .Should().BeFalse();
        error.Should().Contain("customers");
    }

    [Fact]
    public void Rejects_mysql_backticks_invented_column()
    {
        var sql = "SELECT `revenue` FROM `sales`";

        SqlSchemaGrounder.TryValidate(sql, "sales", SalesColumns, out var error)
            .Should().BeFalse();
        error.Should().Contain("revenue");
    }

    [Fact]
    public void Rejects_query_with_no_from_table()
    {
        SqlSchemaGrounder.TryValidate("SELECT 1", "sales", SalesColumns, out var error)
            .Should().BeFalse();
        error.Should().Contain("sales");
    }
}
