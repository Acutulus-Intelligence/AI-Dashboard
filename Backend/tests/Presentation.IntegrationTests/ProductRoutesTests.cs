using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTos.Request;
using Domain.Enums;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class ProductRoutesTests
{
    private readonly ApiFactory _factory;

    public ProductRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Product_routes_require_subscription()
    {
        var client = CreateClient();
        var email = $"nosub_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        (await client.GetAsync("/api/connections")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/charts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/dashboards")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Connections_schema_graphs_charts_dashboards_happy_paths()
    {
        var client = CreateClient();
        var email = $"prod_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var createConn = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Sample",
            DbProvider.PostgreSql,
            _factory.ExternalHost,
            _factory.ExternalPort,
            _factory.ExternalDatabase,
            _factory.ExternalUsername,
            _factory.ExternalPassword));
        createConn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var connDoc = JsonDocument.Parse(await createConn.Content.ReadAsStringAsync());
        var connectionId = connDoc.RootElement.GetProperty("id").GetGuid();

        (await client.GetAsync("/api/connections")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/connections/{connectionId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var test = await client.PostAsync($"/api/connections/{connectionId}/test", null);
        test.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync($"/api/connections/{connectionId}/tables")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/connections/{connectionId}/tables/sales")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/connections/{connectionId}/tables/sales/preview?rows=5")).StatusCode.Should().Be(HttpStatusCode.OK);

        var generate = await client.PostAsJsonAsync("/api/graphs/generate",
            new GenerateChartRequest(connectionId, "sales", "Show sales by category", null, "prompt"));
        generate.StatusCode.Should().Be(HttpStatusCode.OK);

        var manual = await client.PostAsJsonAsync("/api/graphs/manual",
            new GenerateChartRequest(connectionId, "sales", "category", "bar", "prefab"));
        manual.StatusCode.Should().Be(HttpStatusCode.OK);

        var saveChart = await client.PostAsJsonAsync("/api/charts", new SaveChartRequest(
            "Sales chart",
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
            connectionId,
            "sales"));
        saveChart.StatusCode.Should().Be(HttpStatusCode.OK);
        using var chartDoc = JsonDocument.Parse(await saveChart.Content.ReadAsStringAsync());
        var chartId = chartDoc.RootElement.GetProperty("id").GetGuid();

        (await client.GetAsync("/api/charts")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/charts/{chartId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var execute = await client.PostAsync($"/api/charts/{chartId}/execute", null);
        execute.StatusCode.Should().Be(HttpStatusCode.OK);

        var dashboard = await client.GetAsync("/api/dashboards");
        dashboard.StatusCode.Should().Be(HttpStatusCode.OK);

        var saveWidgets = await client.PutAsJsonAsync("/api/dashboards/widgets", new SaveWidgetsRequest(
        [
            new WidgetItem(null, WidgetType.Chart, chartId, null, null, null, null, 0, 0, 4, 3),
            new WidgetItem(null, WidgetType.Text, null, "Hello", TextVariant.Header, TextHorizontalAlignment.Left, TextVerticalAlignment.Top, 4, 0, 2, 1),
        ]));
        saveWidgets.StatusCode.Should().Be(HttpStatusCode.OK);

        var clearWidgets = await client.PutAsJsonAsync("/api/dashboards/widgets", new SaveWidgetsRequest([]));
        clearWidgets.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/charts/{chartId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/connections/{connectionId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Individual_users_can_only_connect_one_database()
    {
        var client = CreateClient();
        var email = $"limit_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var first = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Only", DbProvider.PostgreSql, _factory.ExternalHost, _factory.ExternalPort,
            _factory.ExternalDatabase, _factory.ExternalUsername, _factory.ExternalPassword));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Second", DbProvider.PostgreSql, _factory.ExternalHost, _factory.ExternalPort,
            _factory.ExternalDatabase, _factory.ExternalUsername, _factory.ExternalPassword));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Product_routes_without_auth_return_401()
    {
        var client = CreateClient();
        (await client.GetAsync("/api/connections")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/charts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/dashboards")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
