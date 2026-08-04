using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTos.Request;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class SqliteAndSqlServerConnectionTests
{
    private readonly ApiFactory _factory;

    public SqliteAndSqlServerConnectionTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Sqlite_connection_lists_tables_previews_and_queries()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sqlite_{Guid.NewGuid():N}.db");
        try
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    """
                    CREATE TABLE sales (category TEXT NOT NULL, amount INTEGER NOT NULL, active INTEGER NOT NULL DEFAULT 1);
                    INSERT INTO sales (category, amount) VALUES ('A', 10), ('B', 20), ('A', 15);
                    """;
                cmd.ExecuteNonQuery();
            }

            var client = CreateClient();
            var email = $"sqlite_{Guid.NewGuid():N}@example.com";
            await client.RegisterAndLoginAsync(email);
            await _factory.SeedActiveSubscriptionAsync(email);

            var create = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
                "Local SQLite",
                DbProvider.Sqlite,
                string.Empty,
                0,
                dbPath,
                string.Empty,
                string.Empty));
            create.StatusCode.Should().Be(HttpStatusCode.OK);
            using var createDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var connectionId = createDoc.RootElement.GetProperty("id").GetGuid();

            var test = await client.PostAsync($"/api/connections/{connectionId}/test", null);
            test.StatusCode.Should().Be(HttpStatusCode.OK);

            var tablesResp = await client.GetAsync($"/api/connections/{connectionId}/tables");
            tablesResp.StatusCode.Should().Be(HttpStatusCode.OK);
            using var tablesDoc = JsonDocument.Parse(await tablesResp.Content.ReadAsStringAsync());
            var tableNames = tablesDoc.RootElement.EnumerateArray()
                .Select(t => t.GetProperty("tableName").GetString())
                .ToList();
            tableNames.Should().Contain("sales");

            var previewResp = await client.GetAsync($"/api/connections/{connectionId}/tables/sales/preview?rows=5");
            previewResp.StatusCode.Should().Be(HttpStatusCode.OK);
            using var previewDoc = JsonDocument.Parse(await previewResp.Content.ReadAsStringAsync());
            previewDoc.RootElement.GetProperty("rows").EnumerateArray().Should().HaveCount(3);

            var configResp = await client.GetAsync($"/api/connections/{connectionId}/config");
            configResp.StatusCode.Should().Be(HttpStatusCode.OK);

            await client.DeleteAsync($"/api/connections/{connectionId}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
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
        sqliteUri.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await sqliteUri.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("provider").GetString().Should().Be("Sqlite");
            doc.RootElement.GetProperty("database").GetString().Should().Be("data/app.db");
        }

        var sqlServerKeyValue = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest("Server=db.example.com,1433;Database=app;User Id=sa;Password=secret"));
        sqlServerKeyValue.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await sqlServerKeyValue.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("host").GetString().Should().Be("db.example.com");
            doc.RootElement.GetProperty("port").GetInt32().Should().Be(1433);
            doc.RootElement.GetProperty("database").GetString().Should().Be("app");
            doc.RootElement.GetProperty("username").GetString().Should().Be("sa");
        }

        var sqliteKeyValue = await client.PostAsJsonAsync("/api/connections/parse",
            new ParseConnectionStringRequest("Data Source=C:\\data\\app.db"));
        sqliteKeyValue.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var doc = JsonDocument.Parse(await sqliteKeyValue.Content.ReadAsStringAsync()))
        {
            doc.RootElement.GetProperty("database").GetString().Should().Be("C:\\data\\app.db");
        }
    }
}