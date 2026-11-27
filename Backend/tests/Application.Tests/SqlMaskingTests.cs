using Application.Services;
using Domain.Enums;
using FluentAssertions;

namespace Application.Tests;

public sealed class SqlMaskingTests
{
    [Fact]
    public void Postgres_array_subscripts_are_not_masked()
    {
        var masked = SqlMasking.MaskLiteralsAndComments(
            "SELECT ARRAY[pg_read_file('/etc/passwd')]",
            DbProvider.PostgreSql);

        // The blocked-function scan must still see the call; otherwise a chart
        // could hide pg_read_file/pg_terminate_backend inside an array subscript.
        masked.Should().Contain("pg_read_file");
    }

    [Fact]
    public void Sql_server_bracket_identifiers_stay_masked()
    {
        var masked = SqlMasking.MaskLiteralsAndComments(
            "SELECT [into] FROM orders",
            DbProvider.SqlServer);

        masked.Should().NotContain("into");
    }

    [Fact]
    public void Redaction_still_masks_bracketed_values()
    {
        var masked = SqlMasking.MaskLiteralsAndComments(
            "SELECT [customer secret] FROM orders",
            provider: null,
            maskAllLineComments: true);

        masked.Should().NotContain("customer secret");
    }
}
