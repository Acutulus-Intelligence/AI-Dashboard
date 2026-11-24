using Application.Services;
using FluentAssertions;

namespace Application.Tests;

public sealed class SqlRedactorTests
{
    [Fact]
    public void Redacts_string_literals_and_comments()
    {
        const string sql =
            "SELECT * FROM orders WHERE note = 'customer secret' -- internal only\nAND amount > 10";

        var redacted = SqlRedactor.Redact(sql);

        redacted.Should().NotContain("customer secret");
        redacted.Should().NotContain("internal only");
        redacted.Should().Contain("SELECT * FROM orders WHERE note =");
        redacted.Should().Contain("AND amount > 10");
    }

    [Fact]
    public void Redacts_line_comments_without_a_space()
    {
        var redacted = SqlRedactor.Redact("SELECT * FROM orders --customer secret\nWHERE id = 1");

        redacted.Should().NotContain("customer secret");
        redacted.Should().Contain("SELECT * FROM orders");
    }

    [Fact]
    public void Keeps_statement_shape_for_debugging()
    {
        var redacted = SqlRedactor.Redact("SELECT category, SUM(amount) AS total FROM sales GROUP BY category");

        redacted.Should().Be("SELECT category, SUM(amount) AS total FROM sales GROUP BY category");
    }

    [Fact]
    public void Truncates_very_long_sql()
    {
        var redacted = SqlRedactor.Redact("SELECT " + new string('x', 1000));

        redacted.Length.Should().BeLessThanOrEqualTo(501);
        redacted.Should().EndWith("…");
    }

    [Fact]
    public void Hash_is_stable_and_short()
    {
        const string sql = "SELECT * FROM orders";

        var first = SqlRedactor.Hash(sql);
        var second = SqlRedactor.Hash(sql);

        first.Should().Be(second);
        first.Should().HaveLength(16);
        first.Should().NotContain("SELECT");
    }
}
