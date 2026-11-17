using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTos.Request;
using Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class ChartCacheRoutesTests
{
    private readonly ApiFactory _factory;

    public ChartCacheRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Execute_serves_cached_rows_until_manual_force_refresh()
    {
        var client = CreateClient();
        var email = $"cache_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var tableName = $"cache_sales_{Guid.NewGuid():N}";
        await ExecuteExternalSqlAsync(
            $"CREATE TABLE {tableName} (category TEXT NOT NULL, amount NUMERIC NOT NULL);" +
            $"INSERT INTO {tableName} (category, amount) VALUES ('A', 10);");

        try
        {
            var connectionId = await CreateConnectionAsync(client);
            var sql = $"SELECT category, SUM(amount) AS amount FROM {tableName} GROUP BY category ORDER BY category";
            var chartId = await CreateChartAsync(client, connectionId, tableName, sql);

            var first = await client.PostAsync($"/api/charts/{chartId}/execute", null);
            first.StatusCode.Should().Be(HttpStatusCode.OK);
            using (var firstDoc = JsonDocument.Parse(await first.Content.ReadAsStringAsync()))
            {
                firstDoc.RootElement.GetProperty("dataUpdatedAt").ValueKind.Should()
                    .NotBe(JsonValueKind.Null);
                firstDoc.RootElement.GetProperty("queryResult")[0]
                    .GetProperty("amount").GetDouble().Should().Be(10);
            }

            await ExecuteExternalSqlAsync(
                $"UPDATE {tableName} SET amount = 99 WHERE category = 'A';");

            var cached = await client.PostAsync($"/api/charts/{chartId}/execute", null);
            cached.StatusCode.Should().Be(HttpStatusCode.OK);
            using (var cachedDoc = JsonDocument.Parse(await cached.Content.ReadAsStringAsync()))
            {
                cachedDoc.RootElement.GetProperty("queryResult")[0]
                    .GetProperty("amount").GetDouble().Should().Be(10);
            }

            var refreshed = await client.PostAsync(
                $"/api/charts/{chartId}/execute?forceRefresh=true&source=manual", null);
            refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
            using (var refreshedDoc = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync()))
            {
                refreshedDoc.RootElement.GetProperty("queryResult")[0]
                    .GetProperty("amount").GetDouble().Should().Be(99);
            }

            (await client.DeleteAsync($"/api/charts/{chartId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
            (await client.DeleteAsync($"/api/connections/{connectionId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await ExecuteExternalSqlAsync($"DROP TABLE IF EXISTS {tableName};");
        }
    }

    [Fact]
    public async Task Updating_a_chart_invalidates_its_cached_rows()
    {
        var client = CreateClient();
        var email = $"cacheedit_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var tableName = $"cache_edit_{Guid.NewGuid():N}";
        await ExecuteExternalSqlAsync(
            $"CREATE TABLE {tableName} (category TEXT NOT NULL, amount NUMERIC NOT NULL);" +
            $"INSERT INTO {tableName} (category, amount) VALUES ('A', 10);");

        try
        {
            var connectionId = await CreateConnectionAsync(client);
            var sql = $"SELECT category, SUM(amount) AS amount FROM {tableName} GROUP BY category ORDER BY category";
            var chartId = await CreateChartAsync(client, connectionId, tableName, sql);

            (await client.PostAsync($"/api/charts/{chartId}/execute", null)).StatusCode.Should()
                .Be(HttpStatusCode.OK);

            await ExecuteExternalSqlAsync(
                $"UPDATE {tableName} SET amount = 77 WHERE category = 'A';");

            var update = await client.PutAsJsonAsync($"/api/charts/{chartId}", new UpdateChartRequest(
                "Cache edit chart updated",
                "bar",
                "category",
                ["amount"],
                "sum",
                "category",
                sql));
            update.StatusCode.Should().Be(HttpStatusCode.OK);

            var afterUpdate = await client.PostAsync($"/api/charts/{chartId}/execute", null);
            afterUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
            using var afterUpdateDoc = JsonDocument.Parse(await afterUpdate.Content.ReadAsStringAsync());
            afterUpdateDoc.RootElement.GetProperty("queryResult")[0]
                .GetProperty("amount").GetDouble().Should().Be(77);

            (await client.DeleteAsync($"/api/charts/{chartId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
            (await client.DeleteAsync($"/api/connections/{connectionId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await ExecuteExternalSqlAsync($"DROP TABLE IF EXISTS {tableName};");
        }
    }

    [Fact]
    public async Task Connection_update_invalidates_cached_rows()
    {
        var client = CreateClient();
        var email = $"cacheconn_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var tableName = $"cache_conn_{Guid.NewGuid():N}";
        await ExecuteExternalSqlAsync(
            $"CREATE TABLE {tableName} (category TEXT NOT NULL, amount NUMERIC NOT NULL);" +
            $"INSERT INTO {tableName} (category, amount) VALUES ('A', 10);");

        try
        {
            var connectionId = await CreateConnectionAsync(client);
            var sql = $"SELECT category, SUM(amount) AS amount FROM {tableName} GROUP BY category";
            var chartId = await CreateChartAsync(client, connectionId, tableName, sql);

            (await client.PostAsync($"/api/charts/{chartId}/execute", null)).StatusCode.Should()
                .Be(HttpStatusCode.OK);

            await ExecuteExternalSqlAsync(
                $"UPDATE {tableName} SET amount = 66 WHERE category = 'A';");

            var update = await client.PutAsJsonAsync($"/api/connections/{connectionId}",
                new UpdateConnectionRequest(
                    "Cache sample updated",
                    DbProvider.PostgreSql,
                    _factory.ExternalConnectionString,
                    ConnectionVisibility.Private));
            update.StatusCode.Should().Be(HttpStatusCode.OK);

            var afterUpdate = await client.PostAsync($"/api/charts/{chartId}/execute", null);
            afterUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
            using var afterUpdateDoc = JsonDocument.Parse(await afterUpdate.Content.ReadAsStringAsync());
            afterUpdateDoc.RootElement.GetProperty("queryResult")[0]
                .GetProperty("amount").GetDouble().Should().Be(66);

            (await client.DeleteAsync($"/api/charts/{chartId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
            (await client.DeleteAsync($"/api/connections/{connectionId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await ExecuteExternalSqlAsync($"DROP TABLE IF EXISTS {tableName};");
        }
    }

    [Fact]
    public async Task Every_forced_execution_is_rate_limited_per_user()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:ChartRefresh:PermitLimit"] = "2",
                    ["RateLimiting:ChartRefresh:WindowSeconds"] = "60",
                });
            });
        });

        var client = limitedFactory.CreateClient(new() { AllowAutoRedirect = false });
        var email = $"cachelimit_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var tableName = $"cache_limit_{Guid.NewGuid():N}";
        await ExecuteExternalSqlAsync(
            $"CREATE TABLE {tableName} (category TEXT NOT NULL, amount NUMERIC NOT NULL);" +
            $"INSERT INTO {tableName} (category, amount) VALUES ('A', 10);");

        try
        {
            var connectionId = await CreateConnectionAsync(client);
            var sql = $"SELECT category, SUM(amount) AS amount FROM {tableName} GROUP BY category";
            var chartId = await CreateChartAsync(client, connectionId, tableName, sql);
            var url = $"/api/charts/{chartId}/execute?forceRefresh=true";

            (await client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.OK);

            // source=auto must not bypass the forced-refresh budget.
            var bypassAttempt = await client.PostAsync($"{url}&source=auto", null);
            bypassAttempt.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            bypassAttempt.Headers.RetryAfter.Should().NotBeNull();

            (await client.DeleteAsync($"/api/charts/{chartId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
            (await client.DeleteAsync($"/api/connections/{connectionId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await ExecuteExternalSqlAsync($"DROP TABLE IF EXISTS {tableName};");
        }
    }

    [Fact]
    public async Task All_execute_requests_are_rate_limited_per_user()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:ChartExecute:PermitLimit"] = "2",
                    ["RateLimiting:ChartExecute:WindowSeconds"] = "60",
                });
            });
        });

        var client = limitedFactory.CreateClient(new() { AllowAutoRedirect = false });
        var email = $"cacheexecutelimit_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var tableName = $"cache_exec_limit_{Guid.NewGuid():N}";
        await ExecuteExternalSqlAsync(
            $"CREATE TABLE {tableName} (category TEXT NOT NULL, amount NUMERIC NOT NULL);" +
            $"INSERT INTO {tableName} (category, amount) VALUES ('A', 10);");

        try
        {
            var connectionId = await CreateConnectionAsync(client);
            var sql = $"SELECT category, SUM(amount) AS amount FROM {tableName} GROUP BY category";
            var chartId = await CreateChartAsync(client, connectionId, tableName, sql);
            var url = $"/api/charts/{chartId}/execute";

            (await client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.PostAsync(url, null)).StatusCode.Should()
                .Be(HttpStatusCode.TooManyRequests);

            (await client.DeleteAsync($"/api/charts/{chartId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
            (await client.DeleteAsync($"/api/connections/{connectionId}")).StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await ExecuteExternalSqlAsync($"DROP TABLE IF EXISTS {tableName};");
        }
    }

    [Fact]
    public async Task Removing_company_member_purges_company_backed_chart_cache()
    {
        var ownerClient = CreateClient();
        var inviteeClient = CreateClient();
        var ownerEmail = $"cacheowner_{Guid.NewGuid():N}@example.com";
        var inviteeEmail = $"cachemember_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);
        await inviteeClient.RegisterAndLoginAsync(inviteeEmail);
        await _factory.SeedActiveSubscriptionAsync(ownerEmail);
        await _factory.SeedActiveSubscriptionAsync(inviteeEmail);

        var tableName = $"cache_member_{Guid.NewGuid():N}";
        await ExecuteExternalSqlAsync(
            $"CREATE TABLE {tableName} (category TEXT NOT NULL, amount NUMERIC NOT NULL);" +
            $"INSERT INTO {tableName} (category, amount) VALUES ('A', 10);");

        try
        {
            var sql = $"SELECT category, SUM(amount) AS amount FROM {tableName} GROUP BY category";

            var privateConnectionId = await CreateConnectionAsync(inviteeClient, $"Private-{Guid.NewGuid():N}");
            var privateChartId = await CreateChartAsync(
                inviteeClient, privateConnectionId, tableName, sql, "Private member chart");
            await ExecuteChartAsync(inviteeClient, privateChartId);

            var companyId = await CreateCompanyAsync(ownerClient, $"CacheCo-{Guid.NewGuid():N}");
            await _factory.SeedCompanySubscriptionAsync(companyId);
            var roleId = await CreateCompanyMemberRoleAsync(ownerClient, companyId);
            await InviteAndAcceptMemberAsync(
                ownerClient, companyId, roleId, inviteeClient, inviteeEmail);

            var companyConnectionId = await CreateConnectionAsync(ownerClient, $"Company-{Guid.NewGuid():N}");
            var companyChartId = await CreateChartAsync(
                inviteeClient, companyConnectionId, tableName, sql, "Company member chart");
            await ExecuteChartAsync(inviteeClient, companyChartId);

            var inviteeId = await _factory.GetUserIdAsync(inviteeEmail);
            var remove = await ownerClient.DeleteAsync(
                $"/api/companies/{companyId}/users/{inviteeId}");
            remove.StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await _factory.GetCachedChartResultAsync(inviteeId, companyChartId))
                .Should().BeNull();
            (await _factory.GetCachedChartResultAsync(inviteeId, privateChartId))
                .Should().NotBeNull();
        }
        finally
        {
            await ExecuteExternalSqlAsync($"DROP TABLE IF EXISTS {tableName};");
        }
    }

    [Fact]
    public async Task Deleting_company_purges_company_backed_chart_cache()
    {
        var ownerClient = CreateClient();
        var inviteeClient = CreateClient();
        var ownerEmail = $"cachedeleteowner_{Guid.NewGuid():N}@example.com";
        var inviteeEmail = $"cachedeleteinvitee_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);
        await inviteeClient.RegisterAndLoginAsync(inviteeEmail);
        await _factory.SeedActiveSubscriptionAsync(ownerEmail);
        await _factory.SeedActiveSubscriptionAsync(inviteeEmail);

        var tableName = $"cache_delete_{Guid.NewGuid():N}";
        await ExecuteExternalSqlAsync(
            $"CREATE TABLE {tableName} (category TEXT NOT NULL, amount NUMERIC NOT NULL);" +
            $"INSERT INTO {tableName} (category, amount) VALUES ('A', 10);");

        try
        {
            var sql = $"SELECT category, SUM(amount) AS amount FROM {tableName} GROUP BY category";

            var ownerPrivateConnectionId = await CreateConnectionAsync(ownerClient, $"OwnerPrivate-{Guid.NewGuid():N}");
            var ownerPrivateChartId = await CreateChartAsync(
                ownerClient, ownerPrivateConnectionId, tableName, sql, "Owner private chart");
            await ExecuteChartAsync(ownerClient, ownerPrivateChartId);

            var inviteePrivateConnectionId = await CreateConnectionAsync(inviteeClient, $"InviteePrivate-{Guid.NewGuid():N}");
            var inviteePrivateChartId = await CreateChartAsync(
                inviteeClient, inviteePrivateConnectionId, tableName, sql, "Invitee private chart");
            await ExecuteChartAsync(inviteeClient, inviteePrivateChartId);

            var companyId = await CreateCompanyAsync(ownerClient, $"CacheDeleteCo-{Guid.NewGuid():N}");
            await _factory.SeedCompanySubscriptionAsync(companyId);
            var roleId = await CreateCompanyMemberRoleAsync(ownerClient, companyId);
            await InviteAndAcceptMemberAsync(
                ownerClient, companyId, roleId, inviteeClient, inviteeEmail);

            var companyConnectionId = await CreateConnectionAsync(ownerClient, $"DeleteCompany-{Guid.NewGuid():N}");
            var ownerCompanyChartId = await CreateChartAsync(
                ownerClient, companyConnectionId, tableName, sql, "Owner company chart");
            var inviteeCompanyChartId = await CreateChartAsync(
                inviteeClient, companyConnectionId, tableName, sql, "Invitee company chart");
            await ExecuteChartAsync(ownerClient, ownerCompanyChartId);
            await ExecuteChartAsync(inviteeClient, inviteeCompanyChartId);

            var deleteCompany = await ownerClient.SendAsync(new HttpRequestMessage(
                HttpMethod.Delete, $"/api/companies/{companyId}")
            {
                Content = JsonContent.Create(new DeleteCompanyRequest(ApiClientExtensions.DefaultPassword)),
            });
            deleteCompany.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var ownerId = await _factory.GetUserIdAsync(ownerEmail);
            var inviteeId = await _factory.GetUserIdAsync(inviteeEmail);

            (await _factory.GetCachedChartResultAsync(ownerId, ownerCompanyChartId))
                .Should().BeNull();
            (await _factory.GetCachedChartResultAsync(inviteeId, inviteeCompanyChartId))
                .Should().BeNull();
            (await _factory.GetCachedChartResultAsync(ownerId, ownerPrivateChartId))
                .Should().NotBeNull();
            (await _factory.GetCachedChartResultAsync(inviteeId, inviteePrivateChartId))
                .Should().NotBeNull();
        }
        finally
        {
            await ExecuteExternalSqlAsync($"DROP TABLE IF EXISTS {tableName};");
        }
    }

    private async Task<Guid> CreateConnectionAsync(HttpClient client, string name = "Cache sample")
    {
        var response = await client.PostAsJsonAsync("/api/connections", new CreateConnectionRequest(
            name,
            DbProvider.PostgreSql,
            _factory.ExternalConnectionString));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateChartAsync(
        HttpClient client,
        Guid connectionId,
        string tableName,
        string sql,
        string title = "Cache chart")
    {
        var response = await client.PostAsJsonAsync("/api/charts", new SaveChartRequest(
            title,
            "bar",
            "category",
            ["amount"],
            "sum",
            "category",
            sql,
            connectionId,
            null,
            tableName));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task ExecuteChartAsync(HttpClient client, Guid chartId)
    {
        var response = await client.PostAsync($"/api/charts/{chartId}/execute", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient ownerClient, string name)
    {
        var response = await ownerClient.PostAsJsonAsync("/api/companies", new CreateCompanyRequest(name));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateCompanyMemberRoleAsync(HttpClient ownerClient, Guid companyId)
    {
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/companies/{companyId}/roles",
            new CreateRoleRequest(
                $"Member-{Guid.NewGuid():N}",
                true,
                false,
                false,
                false));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task InviteAndAcceptMemberAsync(
        HttpClient ownerClient,
        Guid companyId,
        Guid roleId,
        HttpClient inviteeClient,
        string inviteeEmail)
    {
        var invite = await ownerClient.PostAsJsonAsync(
            $"/api/companies/{companyId}/invite",
            new InviteUserRequest(inviteeEmail, roleId));
        invite.StatusCode.Should().Be(HttpStatusCode.OK);

        var pending = await inviteeClient.GetAsync("/api/invites/pending");
        pending.StatusCode.Should().Be(HttpStatusCode.OK);

        using var pendingDoc = JsonDocument.Parse(await pending.Content.ReadAsStringAsync());
        var inviteId = pendingDoc.RootElement
            .EnumerateArray()
            .First(item => item.GetProperty("email").GetString() == inviteeEmail && !item.GetProperty("isAccepted").GetBoolean())
            .GetProperty("id")
            .GetGuid();

        var accept = await inviteeClient.PostAsJsonAsync(
            "/api/companies/accept-invite",
            new AcceptInviteRequest(inviteId));
        accept.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task ExecuteExternalSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_factory.ExternalConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
