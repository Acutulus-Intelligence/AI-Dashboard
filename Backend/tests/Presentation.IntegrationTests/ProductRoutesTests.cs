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
        (await client.GetAsync("/api/chart-folders")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
            _factory.ExternalConnectionString));
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

        var refine = await client.PostAsJsonAsync("/api/graphs/generate",
            new GenerateChartRequest(
                connectionId,
                "sales",
                "Make the title clearer",
                null,
                "prompt",
                new ChartBaseline(
                    "Sales by category",
                    "bar",
                    "category",
                    ["amount"],
                    "sum",
                    "category",
                    "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category")));
        refine.StatusCode.Should().Be(HttpStatusCode.OK);
        using var refineDoc = JsonDocument.Parse(await refine.Content.ReadAsStringAsync());
        refineDoc.RootElement.GetProperty("title").GetString().Should().Be("Sales by category (refined)");

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
            null,
            "sales"));
        saveChart.StatusCode.Should().Be(HttpStatusCode.OK);
        using var chartDoc = JsonDocument.Parse(await saveChart.Content.ReadAsStringAsync());
        var chartId = chartDoc.RootElement.GetProperty("id").GetGuid();

        var updateChart = await client.PutAsJsonAsync($"/api/charts/{chartId}", new UpdateChartRequest(
            "Sales chart updated",
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category ORDER BY amount DESC"));
        updateChart.StatusCode.Should().Be(HttpStatusCode.OK);
        using var updatedDoc = JsonDocument.Parse(await updateChart.Content.ReadAsStringAsync());
        updatedDoc.RootElement.GetProperty("title").GetString().Should().Be("Sales chart updated");
        updatedDoc.RootElement.GetProperty("sqlQuery").GetString().Should()
            .Contain("ORDER BY amount DESC");

        (await client.GetAsync("/api/charts")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/charts/{chartId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var execute = await client.PostAsync($"/api/charts/{chartId}/execute", null);
        execute.StatusCode.Should().Be(HttpStatusCode.OK);

        var createDashboard = await client.PostAsJsonAsync("/api/dashboards", new CreateDashboardRequest("Sales board"));
        createDashboard.StatusCode.Should().Be(HttpStatusCode.OK);
        using var dashboardDoc = JsonDocument.Parse(await createDashboard.Content.ReadAsStringAsync());
        var dashboardId = dashboardDoc.RootElement.GetProperty("id").GetGuid();

        (await client.GetAsync("/api/dashboards")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/dashboards/{dashboardId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var saveWidgets = await client.PutAsJsonAsync($"/api/dashboards/{dashboardId}/widgets", new SaveWidgetsRequest(
        [
            new WidgetItem(null, WidgetType.Chart, chartId, null, null, null, null, 0, 0, 4, 3),
            new WidgetItem(null, WidgetType.Text, null, "Hello", TextVariant.Header, TextHorizontalAlignment.Left, TextVerticalAlignment.Top, 4, 0, 2, 1),
        ]));
        saveWidgets.StatusCode.Should().Be(HttpStatusCode.OK);

        var clearWidgets = await client.PutAsJsonAsync($"/api/dashboards/{dashboardId}/widgets", new SaveWidgetsRequest([]));
        clearWidgets.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PutAsJsonAsync($"/api/dashboards/{dashboardId}", new RenameDashboardRequest("Sales board renamed")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/dashboards/{dashboardId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.DeleteAsync($"/api/charts/{chartId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/connections/{connectionId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Chart_folders_organize_charts_and_survive_folder_delete()
    {
        var client = CreateClient();
        var email = $"folders_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var createConn = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "FolderSample",
            DbProvider.PostgreSql,
            _factory.ExternalConnectionString));
        createConn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var connDoc = JsonDocument.Parse(await createConn.Content.ReadAsStringAsync());
        var connectionId = connDoc.RootElement.GetProperty("id").GetGuid();

        var saveChart = await client.PostAsJsonAsync("/api/charts", new SaveChartRequest(
            "Folder chart",
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            "SELECT category, SUM(amount) AS amount FROM sales GROUP BY category",
            connectionId,
            null,
            "sales"));
        saveChart.StatusCode.Should().Be(HttpStatusCode.OK);
        using var chartDoc = JsonDocument.Parse(await saveChart.Content.ReadAsStringAsync());
        var chartId = chartDoc.RootElement.GetProperty("id").GetGuid();

        var empty = await client.GetAsync("/api/chart-folders");
        empty.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var emptyDoc = JsonDocument.Parse(await empty.Content.ReadAsStringAsync()))
            emptyDoc.RootElement.GetArrayLength().Should().Be(0);

        var createFolder = await client.PostAsJsonAsync("/api/chart-folders", new CreateChartFolderRequest("Reports"));
        createFolder.StatusCode.Should().Be(HttpStatusCode.OK);
        using var folderDoc = JsonDocument.Parse(await createFolder.Content.ReadAsStringAsync());
        var folderId = folderDoc.RootElement.GetProperty("id").GetGuid();

        var duplicate = await client.PostAsJsonAsync("/api/chart-folders", new CreateChartFolderRequest("Reports"));
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var move = await client.PutAsJsonAsync($"/api/charts/{chartId}/folder", new MoveChartRequest(folderId));
        move.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterMove = await client.GetAsync($"/api/charts/{chartId}");
        using (var movedDoc = JsonDocument.Parse(await afterMove.Content.ReadAsStringAsync()))
            movedDoc.RootElement.GetProperty("folderId").GetGuid().Should().Be(folderId);

        var list = await client.GetAsync("/api/chart-folders");
        using (var listDoc = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            var first = listDoc.RootElement.EnumerateArray().First();
            first.GetProperty("name").GetString().Should().Be("Reports");
            first.GetProperty("chartCount").GetInt32().Should().Be(1);
        }

        var rename = await client.PutAsJsonAsync($"/api/chart-folders/{folderId}", new UpdateChartFolderRequest("Dashboards"));
        rename.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/chart-folders/{folderId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var chartAfterDelete = await client.GetAsync($"/api/charts/{chartId}");
        chartAfterDelete.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var unfiledDoc = JsonDocument.Parse(await chartAfterDelete.Content.ReadAsStringAsync()))
            unfiledDoc.RootElement.GetProperty("folderId").ValueKind.Should().Be(JsonValueKind.Null);

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
            "Only", DbProvider.PostgreSql, _factory.ExternalConnectionString));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            "Second", DbProvider.PostgreSql, _factory.ExternalConnectionString));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Product_routes_without_auth_return_401()
    {
        var client = CreateClient();
        (await client.GetAsync("/api/connections")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/charts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/chart-folders")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/dashboards")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
