using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTos.Request;
using Domain.Enums;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class SqliteAndSqlServerConnectionTests
{
    private readonly ApiFactory _factory;

    public SqliteAndSqlServerConnectionTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Create_rejects_sqlite_connection_provider()
    {
        var client = CreateClient();
        var email = $"sqlite_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var dbPath = Path.Combine(Path.GetTempPath(), $"sqlite_{Guid.NewGuid():N}.db");
        var create = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Local SQLite",
            DbProvider.Sqlite,
            $"Data Source={dbPath}"));
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Parse_connection_string_supports_sql_server_and_sqlite()
    {
        var client = CreateClient();
        var email = $"parse_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var mssqlUri = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest("sqlserver://sa:secret@db.example.com:1433/analytics"));
        mssqlUri.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await mssqlUri.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("provider").GetString().Should().Be("SqlServer");
            doc.RootElement.GetProperty("host").GetString().Should().Be("db.example.com");
            doc.RootElement.GetProperty("port").GetInt32().Should().Be(1433);
            doc.RootElement.GetProperty("database").GetString().Should().Be("analytics");
            doc.RootElement.GetProperty("username").GetString().Should().Be("sa");
            doc.RootElement.GetProperty("password").GetString().Should().Be("secret");
        }

        var sqliteUri = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest("sqlite:///data/app.db"));
        sqliteUri.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sqlServerKeyValue = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest("Server=db.example.com,1433;Database=app;User Id=sa;Password=secret"));
        sqlServerKeyValue.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await sqlServerKeyValue.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("provider").GetString().Should().Be("SqlServer");
            doc.RootElement.GetProperty("host").GetString().Should().Be("db.example.com");
            doc.RootElement.GetProperty("port").GetInt32().Should().Be(1433);
            doc.RootElement.GetProperty("database").GetString().Should().Be("app");
            doc.RootElement.GetProperty("username").GetString().Should().Be("sa");
        }
    }

    [Fact]
    public async Task Parse_key_value_detects_postgres_and_mysql_providers()
    {
        var client = CreateClient();
        var email = $"kv_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var postgres = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest("Host=localhost;Port=5432;Database=x;Username=u;Password=p"));
        postgres.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await postgres.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("provider").GetString().Should().Be("PostgreSql");
            doc.RootElement.GetProperty("host").GetString().Should().Be("localhost");
            doc.RootElement.GetProperty("username").GetString().Should().Be("u");
        }

        var mySql = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest("Server=localhost;Port=3306;Database=shop;Uid=root;Pwd=secret"));
        mySql.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await mySql.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("provider").GetString().Should().Be("MySql");
            doc.RootElement.GetProperty("host").GetString().Should().Be("localhost");
        }

        var sqlite = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest(@"Data Source=C:\data\app.db"));
        sqlite.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_rejects_connection_string_that_does_not_match_provider()
    {
        var client = CreateClient();
        var email = $"mismatch_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var create = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Wrong provider",
            DbProvider.PostgreSql,
            "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret"));
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Parse_detects_sql_server_from_data_source_and_integrated_security()
    {
        var client = CreateClient();
        var email = $"ds_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var parse = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest(
                "Data Source=localhost; Database=ActivityGO; Integrated Security=True; Trust Server Certificate=True;"));
        parse.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await parse.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("provider").GetString().Should().Be("SqlServer");
            doc.RootElement.GetProperty("host").GetString().Should().Be("localhost");
            doc.RootElement.GetProperty("database").GetString().Should().Be("ActivityGO");
        }

        var create = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Wrong provider",
            DbProvider.PostgreSql,
            "Data Source=localhost; Database=ActivityGO; Integrated Security=True; Trust Server Certificate=True;"));
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Config_returns_stored_connection_string_verbatim()
    {
        var client = CreateClient();
        var email = $"cfg_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var create = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Round trip",
            DbProvider.PostgreSql,
            _factory.ExternalConnectionString));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        using var createDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var connectionId = createDoc.RootElement.GetProperty("id").GetGuid();

        var configResp = await client.GetAsync($"/api/connections/{connectionId}/config");
        configResp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var configDoc = JsonDocument.Parse(await configResp.Content.ReadAsStringAsync());
        configDoc.RootElement.GetProperty("connectionString").GetString().Should().Be(_factory.ExternalConnectionString);

        await client.DeleteAsync($"/api/connections/{connectionId}");
    }

    [Fact]
    public async Task Create_rejects_connection_string_without_detectable_provider()
    {
        var client = CreateClient();
        var email = $"amb_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var create = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Ambiguous",
            DbProvider.PostgreSql,
            "Server=localhost;Database=app;Username=u;Password=p"));
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Parse_accepts_connection_string_without_credentials()
    {
        var client = CreateClient();
        var email = $"nocreds_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var noCreds = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest("postgres://db.example.com:5432/analytics"));
        noCreds.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await noCreds.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("provider").GetString().Should().Be("PostgreSql");
            doc.RootElement.GetProperty("host").GetString().Should().Be("db.example.com");
            doc.RootElement.GetProperty("database").GetString().Should().Be("analytics");
            doc.RootElement.GetProperty("username").GetString().Should().Be("");
            doc.RootElement.GetProperty("password").GetString().Should().Be("");
        }
    }
}