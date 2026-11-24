using Domain.Enums;
using FluentAssertions;
using Infrastructure.ExternalDb.Services;

namespace Infrastructure.Tests;

public sealed class ConnectionConfigParsingTests
{
    [Fact]
    public void Reads_postgres_password_with_semicolon()
    {
        var ok = ConnectionStringParts.TryRead(
            DbProvider.PostgreSql,
            "Host=db.example.com;Port=5432;Database=app;Username=u;Password=\"a;b\"",
            out var host,
            out var port,
            out var database,
            out var username,
            out var password);

        ok.Should().BeTrue();
        host.Should().Be("db.example.com");
        port.Should().Be(5432);
        database.Should().Be("app");
        username.Should().Be("u");
        password.Should().Be("a;b");
    }

    [Fact]
    public void Reads_sql_server_named_instance()
    {
        var ok = ConnectionStringParts.TryRead(
            DbProvider.SqlServer,
            @"Server=db.example.com\SQLEXPRESS;Database=app;User Id=sa;Password=secret",
            out var host,
            out var port,
            out _,
            out var username,
            out var password);

        ok.Should().BeTrue();
        host.Should().Be(@"db.example.com\SQLEXPRESS");
        port.Should().Be(0);
        username.Should().Be("sa");
        password.Should().Be("secret");
    }

    [Fact]
    public void Reads_mysql_parts()
    {
        var ok = ConnectionStringParts.TryRead(
            DbProvider.MySql,
            "Server=db.example.com;Port=3306;Database=shop;User ID=root;Password=secret",
            out var host,
            out var port,
            out var database,
            out var username,
            out var password);

        ok.Should().BeTrue();
        host.Should().Be("db.example.com");
        port.Should().Be(3306);
        database.Should().Be("shop");
        username.Should().Be("root");
        password.Should().Be("secret");
    }

    [Fact]
    public void Reads_uri_parts_and_decodes_the_password()
    {
        var ok = ConnectionStringParts.TryRead(
            DbProvider.PostgreSql,
            "postgres://u:p%3Bq@db.example.com:5432/app",
            out var host,
            out var port,
            out var database,
            out var username,
            out var password);

        ok.Should().BeTrue();
        host.Should().Be("db.example.com");
        port.Should().Be(5432);
        database.Should().Be("app");
        username.Should().Be("u");
        password.Should().Be("p;q");
    }

    [Fact]
    public void Returns_false_for_unreadable_connection_string()
    {
        var ok = ConnectionStringParts.TryRead(
            DbProvider.PostgreSql,
            "this is not a connection string",
            out _,
            out _,
            out _,
            out _,
            out _);

        ok.Should().BeFalse();
    }
}
