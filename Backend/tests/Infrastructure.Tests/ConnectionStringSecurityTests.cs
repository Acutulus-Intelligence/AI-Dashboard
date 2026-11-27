using Domain.Enums;
using FluentAssertions;
using Infrastructure.ExternalDb.Services;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using Npgsql;

namespace Infrastructure.Tests;

public sealed class ConnectionStringSecurityTests
{
    [Theory]
    [InlineData("Integrated Security=SSPI")]
    [InlineData("Integrated Security=true")]
    [InlineData("Trusted_Connection=yes")]
    public void Rejects_windows_authentication_variants(string auth)
    {
        var input = $"Server=db.example.com,1433;Database=app;User Id=sa;Password=secret;{auth}";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("Windows authentication");
    }

    [Fact]
    public void Rejects_optional_sql_server_encryption()
    {
        var input = "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret;Encrypt=Optional";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("Encrypt");
    }

    [Fact]
    public void Rejects_integrated_security()
    {
        var input = "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret;Integrated Security=true";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("Windows authentication");
    }

    [Fact]
    public void Rejects_attach_db_filename()
    {
        var input = "Server=(localdb)\\mssqllocaldb;AttachDbFilename=C:\\data\\app.mdf;Integrated Security=false";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("AttachDbFilename");
    }

    [Fact]
    public void Rejects_disabled_sql_server_encryption()
    {
        var input = "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret;Encrypt=false";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("Encrypt=false");
    }

    [Fact]
    public void Rejects_trust_server_certificate()
    {
        var input = "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret;TrustServerCertificate=true";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("TrustServerCertificate");
    }

    [Fact]
    public void Rejects_mysql_local_infile()
    {
        var input = "Server=db.example.com;Database=shop;Uid=root;Pwd=secret;AllowLoadLocalInfile=true";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.MySql, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("AllowLoadLocalInfile");
    }

    [Fact]
    public void Rejects_postgres_tls_disabled()
    {
        var input = "Host=db.example.com;Port=5432;Database=app;Username=u;Password=p;SSL Mode=Disable";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.PostgreSql, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("TLS");
    }

    [Fact]
    public void Forces_postgres_tls_when_required()
    {
        var input = "Host=db.example.com;Port=5432;Database=app;Username=u;Password=p";

        ConnectionStringSecurity.TryNormalize(
            input,
            DbProvider.PostgreSql,
            requireTls: true,
            out var normalized,
            out var error,
            verifyServerCertificate: false)
            .Should().BeTrue(error);

        new NpgsqlConnectionStringBuilder(normalized).SslMode.Should().Be(Npgsql.SslMode.Require);
    }

    [Fact]
    public void Verifies_postgres_certificate_when_required()
    {
        var input = "Host=db.example.com;Port=5432;Database=app;Username=u;Password=p";

        ConnectionStringSecurity.TryNormalize(
            input, DbProvider.PostgreSql, requireTls: true, out var normalized, out var error)
            .Should().BeTrue(error);

        new NpgsqlConnectionStringBuilder(normalized).SslMode.Should().Be(Npgsql.SslMode.VerifyFull);
    }

    [Fact]
    public void Keeps_postgres_default_when_tls_is_not_required()
    {
        var input = "Host=db.example.com;Port=5432;Database=app;Username=u;Password=p";

        ConnectionStringSecurity.TryNormalize(
            input, DbProvider.PostgreSql, requireTls: false, out var normalized, out var error)
            .Should().BeTrue(error);

        new NpgsqlConnectionStringBuilder(normalized).SslMode.Should().Be(Npgsql.SslMode.Prefer);
    }

    [Fact]
    public void Forces_mysql_tls_when_required()
    {
        var input = "Server=db.example.com;Database=shop;Uid=root;Pwd=secret";

        ConnectionStringSecurity.TryNormalize(
            input,
            DbProvider.MySql,
            requireTls: true,
            out var normalized,
            out var error,
            verifyServerCertificate: false)
            .Should().BeTrue(error);

        new MySqlConnectionStringBuilder(normalized).SslMode.Should().Be(MySqlSslMode.Required);
    }

    [Fact]
    public void Verifies_mysql_certificate_when_required()
    {
        var input = "Server=db.example.com;Database=shop;Uid=root;Pwd=secret";

        ConnectionStringSecurity.TryNormalize(
            input, DbProvider.MySql, requireTls: true, out var normalized, out var error)
            .Should().BeTrue(error);

        new MySqlConnectionStringBuilder(normalized).SslMode.Should().Be(MySqlSslMode.VerifyFull);
    }

    [Fact]
    public void Preserves_strict_sql_server_encryption()
    {
        var input = "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret;Encrypt=Strict";

        ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: false, out var normalized, out var error)
            .Should().BeTrue(error);

        new SqlConnectionStringBuilder(normalized).Encrypt
            .Should().Be(SqlConnectionEncryptOption.Strict);
    }

    [Fact]
    public void Disables_sql_server_certificate_verification_when_requested()
    {
        var input = "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret";

        ConnectionStringSecurity.TryNormalize(
            input,
            DbProvider.SqlServer,
            requireTls: true,
            out var normalized,
            out var error,
            verifyServerCertificate: false)
            .Should().BeTrue(error);

        var builder = new SqlConnectionStringBuilder(normalized);
        builder.TrustServerCertificate.Should().BeTrue();
        builder.Encrypt.Should().Be(SqlConnectionEncryptOption.Mandatory);
    }

    [Fact]
    public void Allows_explicit_trust_server_certificate_when_verification_is_disabled()
    {
        var input = "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret;TrustServerCertificate=true";

        ConnectionStringSecurity.TryNormalize(
            input,
            DbProvider.SqlServer,
            requireTls: true,
            out var normalized,
            out var error,
            verifyServerCertificate: false)
            .Should().BeTrue(error);

        new SqlConnectionStringBuilder(normalized).TrustServerCertificate.Should().BeTrue();
    }

    [Fact]
    public void Forces_sql_server_encryption()
    {
        var input = "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret";

        ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: false, out var normalized, out var error)
            .Should().BeTrue(error);

        var builder = new SqlConnectionStringBuilder(normalized);
        builder.Encrypt.Should().Be(SqlConnectionEncryptOption.Mandatory);
        builder.TrustServerCertificate.Should().BeFalse();
    }

    [Fact]
    public void Rejects_unknown_driver_keys()
    {
        var input = "Host=db.example.com;Port=5432;Database=app;Username=u;Password=p;BogusKey=true";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.PostgreSql, requireTls: false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("unsupported setting");
    }

    [Fact]
    public void Allows_quoted_password_with_semicolon()
    {
        var input = "Host=db.example.com;Port=5432;Database=app;Username=u;Password=\"a;b\"";

        ConnectionStringSecurity.TryNormalize(
            input, DbProvider.PostgreSql, requireTls: false, out var normalized, out var error)
            .Should().BeTrue(error);

        new NpgsqlConnectionStringBuilder(normalized).Password.Should().Be("a;b");
    }

    [Fact]
    public void Rejects_uri_with_tls_disabled()
    {
        var input = "postgres://u:p@db.example.com:5432/app?sslmode=disable";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.PostgreSql, requireTls: true, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("TLS");
    }

    [Fact]
    public void Normalizes_postgres_uri_and_enforces_tls_verification()
    {
        var input = "postgres://u:p@db.example.com:5432/app?application_name=ai-dashboard";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.PostgreSql, requireTls: true, out var normalized, out var error);

        ok.Should().BeTrue(error);
        var builder = new NpgsqlConnectionStringBuilder(normalized);
        builder.Host.Should().Be("db.example.com");
        builder.Port.Should().Be(5432);
        builder.Database.Should().Be("app");
        builder.Username.Should().Be("u");
        builder.Password.Should().Be("p");
        builder.ApplicationName.Should().Be("ai-dashboard");
        builder.SslMode.Should().Be(Npgsql.SslMode.VerifyFull);
    }

    [Fact]
    public void Normalizes_postgres_uri_with_hyphenated_sslmode()
    {
        var input = "postgres://u:p@db.example.com:5432/app?sslmode=verify-full";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.PostgreSql, requireTls: true, out var normalized, out var error);

        ok.Should().BeTrue(error);
        new NpgsqlConnectionStringBuilder(normalized).SslMode.Should().Be(Npgsql.SslMode.VerifyFull);
    }

    [Fact]
    public void Normalizes_mysql_uri_and_enforces_tls()
    {
        var input = "mysql://root:secret@db.example.com:3306/shop";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.MySql, requireTls: true, out var normalized, out var error);

        ok.Should().BeTrue(error);
        var builder = new MySqlConnectionStringBuilder(normalized);
        builder.Server.Should().Be("db.example.com");
        builder.Port.Should().Be(3306);
        builder.Database.Should().Be("shop");
        builder.UserID.Should().Be("root");
        builder.SslMode.Should().Be(MySqlSslMode.VerifyFull);
    }

    [Fact]
    public void Normalizes_sql_server_uri_and_preserves_strict_encryption()
    {
        var input = "mssql://sa:secret@db.example.com:1433/app?encrypt=strict";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.SqlServer, requireTls: true, out var normalized, out var error);

        ok.Should().BeTrue(error);
        var builder = new SqlConnectionStringBuilder(normalized);
        builder.DataSource.Should().Be("db.example.com,1433");
        builder.InitialCatalog.Should().Be("app");
        builder.Encrypt.Should().Be(SqlConnectionEncryptOption.Strict);
        builder.TrustServerCertificate.Should().BeFalse();
    }

    [Fact]
    public void Rejects_uri_for_a_different_provider()
    {
        var input = "postgres://u:p@db.example.com:5432/app";

        var ok = ConnectionStringSecurity.TryNormalize(
            input, DbProvider.MySql, requireTls: true, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("does not match");
    }
}
