using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTos.Request;
using Application.Interfaces;
using Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class SecurityHardeningRoutesTests
{
    private readonly ApiFactory _factory;

    public SecurityHardeningRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    private async Task<Guid> CreateConnectionAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            $"Sec-{Guid.NewGuid():N}",
            DbProvider.PostgreSql,
            _factory.ExternalConnectionString));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Saving_a_chart_with_select_into_is_rejected()
    {
        var client = CreateClient();
        var email = $"sqlsafety_save_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);
        var connectionId = await CreateConnectionAsync(client);

        var save = await client.PostAsJsonAsync("/api/charts", new SaveChartRequest(
            "Unsafe SELECT INTO",
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            "SELECT * INTO backup FROM sales",
            connectionId,
            null,
            "sales"));

        save.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_a_chart_with_select_into_is_rejected()
    {
        var client = CreateClient();
        var email = $"sqlsafety_update_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);
        var connectionId = await CreateConnectionAsync(client);

        var save = await client.PostAsJsonAsync("/api/charts", new SaveChartRequest(
            "Safe chart",
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
            connectionId,
            null,
            "sales"));

        save.StatusCode.Should().Be(HttpStatusCode.OK);
        using var savedDoc = JsonDocument.Parse(await save.Content.ReadAsStringAsync());
        var chartId = savedDoc.RootElement.GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/charts/{chartId}", new UpdateChartRequest(
            "Unsafe update",
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            "SELECT * INTO backup FROM sales"));

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Connection_config_does_not_return_the_password()
    {
        var client = CreateClient();
        var email = $"cfg_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);
        var connectionId = await CreateConnectionAsync(client);

        var response = await client.GetAsync($"/api/connections/{connectionId}/config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(_factory.ExternalPassword);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("passwordSet").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("username").GetString().Should().Be(_factory.ExternalUsername);
        doc.RootElement.GetProperty("maskedConnectionString").GetString().Should().Contain("Password=***");
    }

    [Fact]
    public async Task Updating_connection_without_a_new_password_keeps_the_existing_one()
    {
        var client = CreateClient();
        var email = $"cfgkeep_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);
        var connectionId = await CreateConnectionAsync(client);

        var update = await client.PutAsJsonAsync($"/api/connections/{connectionId}", new UpdateConnectionRequest(
            "Renamed connection",
            DbProvider.PostgreSql,
            "",
            ConnectionVisibility.Private,
            [],
            KeepExistingPassword: true));

        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var test = await client.PostAsync($"/api/connections/{connectionId}/test", null);
        test.StatusCode.Should().Be(HttpStatusCode.OK);
        using var testDoc = JsonDocument.Parse(await test.Content.ReadAsStringAsync());
        testDoc.RootElement.GetProperty("isVerified").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Generate_save_and_execute_are_audited()
    {
        _factory.FakeAudit.Clear();

        var client = CreateClient();
        var email = $"audit_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);
        var connectionId = await CreateConnectionAsync(client);

        var generate = await client.PostAsJsonAsync("/api/graphs/generate",
            new GenerateChartRequest(connectionId, "sales", "Show sales by category", null, "prompt"));
        generate.StatusCode.Should().Be(HttpStatusCode.OK);

        var save = await client.PostAsJsonAsync("/api/charts", new SaveChartRequest(
            "Audited chart",
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
            connectionId,
            null,
            "sales"));
        save.StatusCode.Should().Be(HttpStatusCode.OK);
        using var savedDoc = JsonDocument.Parse(await save.Content.ReadAsStringAsync());
        var chartId = savedDoc.RootElement.GetProperty("id").GetGuid();

        var execute = await client.PostAsync($"/api/charts/{chartId}/execute", null);
        execute.StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = _factory.FakeAudit.Entries;
        entries.Should().Contain(e =>
            e.Action == AuditAction.ChartGenerated && e.ConnectionId == connectionId);
        entries.Should().Contain(e =>
            e.Action == AuditAction.ChartSaved && e.ChartId == chartId);
        entries.Should().Contain(e =>
            e.Action == AuditAction.ChartExecuted && e.ChartId == chartId);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/charts/catalog");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
        response.Headers
            .GetValues("Content-Security-Policy")
            .Should()
            .ContainSingle()
            .Which.Should()
            .Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task Creating_a_connection_with_windows_auth_is_rejected()
    {
        var client = CreateClient();
        var email = $"connsec_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var create = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Unsafe SQL Server",
            DbProvider.SqlServer,
            "Server=db.example.com,1433;Database=app;User Id=sa;Password=secret;Integrated Security=true"));

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creating_a_connection_to_localhost_is_rejected_by_default()
    {
        using var secureFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ExternalDb:AllowPrivateHosts"] = "false",
                });
            });
        });

        var client = secureFactory.CreateClient(new() { AllowAutoRedirect = false });
        var email = $"ssrf_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var create = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Internal host",
            DbProvider.PostgreSql,
            _factory.ExternalConnectionString));

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ai_generation_is_rate_limited_per_user()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:AiGenerate:PermitLimit"] = "2",
                    ["RateLimiting:AiGenerate:WindowSeconds"] = "60",
                });
            });
        });

        var client = limitedFactory.CreateClient(new() { AllowAutoRedirect = false });
        var email = $"ailimit_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);
        var connectionId = await CreateConnectionAsync(client);

        var request = new GenerateChartRequest(
            connectionId, "sales", "Show sales by category", null, "prompt");

        (await client.PostAsJsonAsync("/api/graphs/generate", request)).StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/graphs/generate", request)).StatusCode.Should()
            .Be(HttpStatusCode.OK);

        var third = await client.PostAsJsonAsync("/api/graphs/generate", request);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter.Should().NotBeNull();

        // Manual generation shares the same per-user bucket.
        var manual = await client.PostAsJsonAsync("/api/graphs/manual", new GenerateChartRequest(
            connectionId, "sales", "category", "bar", "prefab"));
        manual.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Ai_generation_limit_is_per_user()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:AiGenerate:PermitLimit"] = "1",
                    ["RateLimiting:AiGenerate:WindowSeconds"] = "60",
                });
            });
        });

        async Task<HttpClient> CreateLimitedUserAsync(string prefix)
        {
            var client = limitedFactory.CreateClient(new() { AllowAutoRedirect = false });
            var email = $"{prefix}_{Guid.NewGuid():N}@example.com";
            await client.RegisterAndLoginAsync(email);
            await _factory.SeedActiveSubscriptionAsync(email);
            return client;
        }

        var first = await CreateLimitedUserAsync("ailimit_a");
        var firstConnection = await CreateConnectionAsync(first);
        var request = new GenerateChartRequest(
            firstConnection, "sales", "Show sales by category", null, "prompt");

        (await first.PostAsJsonAsync("/api/graphs/generate", request)).StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await first.PostAsJsonAsync("/api/graphs/generate", request)).StatusCode.Should()
            .Be(HttpStatusCode.TooManyRequests);

        // Another user has an independent bucket.
        var second = await CreateLimitedUserAsync("ailimit_b");
        var secondConnection = await CreateConnectionAsync(second);
        var secondRequest = new GenerateChartRequest(
            secondConnection, "sales", "Show sales by category", null, "prompt");

        (await second.PostAsJsonAsync("/api/graphs/generate", secondRequest)).StatusCode.Should()
            .Be(HttpStatusCode.OK);
    }
}
