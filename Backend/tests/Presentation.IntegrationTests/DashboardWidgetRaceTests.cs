using System.Net;
using System.Net.Http.Json;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class DashboardWidgetRaceTests
{
    private readonly ApiFactory _factory;

    public DashboardWidgetRaceTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Saving_a_widget_for_a_chart_deleted_during_the_save_returns_a_conflict()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var email = $"widgetrace_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);
        var userId = await _factory.GetUserIdAsync(email);
        var chartId = await _factory.SeedSavedChartAsync(userId, "Race chart");

        var create = await client.PostAsJsonAsync("/api/dashboards", new CreateDashboardRequest("Race board"));
        var dashboard = await create.ReadJsonAsync<DashboardResponse>();

        // A trigger simulates the chart disappearing between validation and save:
        // the insert then hits the FK to saved_charts. Without the service mapping
        // that would surface as a 500 instead of a client-facing conflict.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE OR REPLACE FUNCTION delete_chart_on_widget_insert() RETURNS trigger AS $$
                BEGIN
                    DELETE FROM saved_charts WHERE "Id" = NEW."SavedChartId";
                    RETURN NEW;
                END; $$ LANGUAGE plpgsql;
                """);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER stale_chart_race BEFORE INSERT ON dashboard_widgets
                FOR EACH ROW EXECUTE FUNCTION delete_chart_on_widget_insert();
                """);
        }

        try
        {
            var widget = new WidgetItem(null, WidgetType.Chart, chartId, null, null, null, null, 0, 0, 4, 3);
            var response = await client.PutAsJsonAsync(
                $"/api/dashboards/{dashboard.Id}/widgets", new SaveWidgetsRequest([widget]));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.Content.ReadAsStringAsync()).Should().Contain("chart_deleted");
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS stale_chart_race ON dashboard_widgets;");
            await db.Database.ExecuteSqlRawAsync("DROP FUNCTION IF EXISTS delete_chart_on_widget_insert();");
        }
    }
}
