using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Enums;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class DashboardRoutesTests
{
    private readonly ApiFactory _factory;

    public DashboardRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    private async Task<Guid> CreateDashboardAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/dashboards", new CreateDashboardRequest(name));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.ReadJsonAsync<DashboardResponse>();
        return dashboard.Id;
    }

    [Fact]
    public async Task Multiple_dashboards_can_be_created_listed_renamed_and_deleted()
    {
        var client = CreateClient();
        var email = $"multidash_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var firstId = await CreateDashboardAsync(client, "Board one");
        var secondId = await CreateDashboardAsync(client, "Board two");

        var list = await (await client.GetAsync("/api/dashboards")).ReadJsonAsync<List<DashboardSummaryResponse>>();
        list.Should().HaveCount(2);
        list.Select(d => d.Id).Should().Contain([firstId, secondId]);

        var rename = await client.PutAsJsonAsync($"/api/dashboards/{firstId}", new RenameDashboardRequest("Board one renamed"));
        rename.StatusCode.Should().Be(HttpStatusCode.OK);
        (await rename.ReadJsonAsync<DashboardResponse>()).Name.Should().Be("Board one renamed");

        (await client.DeleteAsync($"/api/dashboards/{secondId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await (await client.GetAsync("/api/dashboards")).ReadJsonAsync<List<DashboardSummaryResponse>>();
        afterDelete.Should().ContainSingle(d => d.Id == firstId);

        // Duplicate names are rejected.
        (await client.PostAsJsonAsync("/api/dashboards", new CreateDashboardRequest("Board one renamed")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Dashboards_are_private_to_their_owner()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"dashowner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);
        await _factory.SeedActiveSubscriptionAsync(ownerEmail);

        var dashboardId = await CreateDashboardAsync(ownerClient, "Private board");

        var outsiderClient = CreateClient();
        var outsiderEmail = $"dashoutsider_{Guid.NewGuid():N}@example.com";
        await outsiderClient.RegisterAndLoginAsync(outsiderEmail);
        await _factory.SeedActiveSubscriptionAsync(outsiderEmail);

        (await outsiderClient.GetAsync($"/api/dashboards/{dashboardId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsiderClient.DeleteAsync($"/api/dashboards/{dashboardId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dashboard_creation_enforces_the_plan_limit()
    {
        var client = CreateClient();
        var email = $"dashlimit_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var userId = await _factory.GetUserIdAsync(email);
        var planId = await _factory.SeedIndividualPlanWithDashboardLimitAsync(1);
        await _factory.SeedSubscriptionForPlanAsync(userId, planId);

        (await client.PostAsJsonAsync("/api/dashboards", new CreateDashboardRequest("Only board")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsJsonAsync("/api/dashboards", new CreateDashboardRequest("Too many")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Transferring_a_dashboard_reassigns_owner_and_its_charts()
    {
        var setup = await CreateCompanyWithMemberAsync();
        var ownerClient = setup.OwnerClient;
        var inviteeClient = setup.InviteeClient;
        var inviteeId = setup.InviteeId;

        var chartId = await _factory.SeedSavedChartAsync(setup.OwnerId, "Owned chart");
        var dashboardId = await CreateDashboardAsync(ownerClient, "Team board");

        var save = await ownerClient.PutAsJsonAsync($"/api/dashboards/{dashboardId}/widgets", new SaveWidgetsRequest(
        [
            new WidgetItem(null, WidgetType.Chart, chartId, null, null, null, null, 0, 0, 4, 3),
        ]));
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var wrongPassword = await ownerClient.PostAsJsonAsync(
            $"/api/dashboards/{dashboardId}/transfer-ownership",
            new TransferDashboardRequest(inviteeId, "WrongPass123!"));
        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var transfer = await ownerClient.PostAsJsonAsync(
            $"/api/dashboards/{dashboardId}/transfer-ownership",
            new TransferDashboardRequest(inviteeId, ApiClientExtensions.DefaultPassword));
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);
        (await transfer.ReadJsonAsync<TransferDashboardResponse>()).Transferred.Should().BeTrue();

        (await _factory.GetDashboardOwnerAsync(dashboardId)).Should().Be(inviteeId);
        (await _factory.GetSavedChartOwnerAsync(chartId)).Should().Be(inviteeId);

        (await ownerClient.GetAsync($"/api/dashboards/{dashboardId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await inviteeClient.GetAsync($"/api/dashboards/{dashboardId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Transferring_a_shared_chart_copies_it_for_the_new_owner()
    {
        var setup = await CreateCompanyWithMemberAsync();
        var chartId = await _factory.SeedSavedChartAsync(setup.OwnerId, "Shared chart");

        var keepId = await CreateDashboardAsync(setup.OwnerClient, "Keep board");
        var moveId = await CreateDashboardAsync(setup.OwnerClient, "Move board");

        var widget = new WidgetItem(null, WidgetType.Chart, chartId, null, null, null, null, 0, 0, 4, 3);
        (await setup.OwnerClient.PutAsJsonAsync($"/api/dashboards/{keepId}/widgets", new SaveWidgetsRequest([widget])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await setup.OwnerClient.PutAsJsonAsync($"/api/dashboards/{moveId}/widgets", new SaveWidgetsRequest([widget])))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var transfer = await setup.OwnerClient.PostAsJsonAsync(
            $"/api/dashboards/{moveId}/transfer-ownership",
            new TransferDashboardRequest(setup.InviteeId, ApiClientExtensions.DefaultPassword));
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);
        (await transfer.ReadJsonAsync<TransferDashboardResponse>()).Transferred.Should().BeTrue();

        // The original chart stays with the seller for the retained board.
        (await _factory.GetSavedChartOwnerAsync(chartId)).Should().Be(setup.OwnerId);

        // The transferred board points at a private copy owned by the new owner.
        var moved = await (await setup.InviteeClient.GetAsync($"/api/dashboards/{moveId}"))
            .ReadJsonAsync<DashboardResponse>();
        var copiedChartId = moved.Widgets.Single(w => w.WidgetType == WidgetType.Chart).SavedChartId!.Value;
        copiedChartId.Should().NotBe(chartId);
        (await _factory.GetSavedChartOwnerAsync(copiedChartId)).Should().Be(setup.InviteeId);
    }

    [Fact]
    public async Task Transferring_a_dashboard_with_a_private_connection_prompts_then_shares_it()
    {
        var setup = await CreateCompanyWithMemberAsync();

        var company = await (await setup.OwnerClient.GetAsync("/api/companies/me"))
            .ReadJsonAsync<CompanyResponse>();

        var connectionId = await _factory.SeedCompanyConnectionAsync(
            setup.OwnerId, company.Id, "Private DB", ConnectionVisibility.Private);
        var chartId = await _factory.SeedSavedChartWithConnectionAsync(
            setup.OwnerId, "Private chart", connectionId);
        var dashboardId = await CreateDashboardAsync(setup.OwnerClient, "Private board");

        var save = await setup.OwnerClient.PutAsJsonAsync($"/api/dashboards/{dashboardId}/widgets", new SaveWidgetsRequest(
        [
            new WidgetItem(null, WidgetType.Chart, chartId, null, null, null, null, 0, 0, 4, 3),
        ]));
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var first = await setup.OwnerClient.PostAsJsonAsync(
            $"/api/dashboards/{dashboardId}/transfer-ownership",
            new TransferDashboardRequest(setup.InviteeId, ApiClientExtensions.DefaultPassword));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var prompt = await first.ReadJsonAsync<TransferDashboardResponse>();
        prompt.Transferred.Should().BeFalse();
        prompt.RequiresSharing.Should().ContainSingle(i => i.Type == "connection" && i.Id == connectionId);

        // Nothing changed until the seller confirms.
        (await _factory.GetConnectionAccessAsync(connectionId)).Visibility.Should().Be(ConnectionVisibility.Private);
        (await _factory.GetDashboardOwnerAsync(dashboardId)).Should().Be(setup.OwnerId);

        var confirmed = await setup.OwnerClient.PostAsJsonAsync(
            $"/api/dashboards/{dashboardId}/transfer-ownership",
            new TransferDashboardRequest(setup.InviteeId, ApiClientExtensions.DefaultPassword, ShareDataSources: true));
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await confirmed.ReadJsonAsync<TransferDashboardResponse>()).Transferred.Should().BeTrue();

        (await _factory.GetDashboardOwnerAsync(dashboardId)).Should().Be(setup.InviteeId);
        (await _factory.GetSavedChartOwnerAsync(chartId)).Should().Be(setup.InviteeId);

        var access = await _factory.GetConnectionAccessAsync(connectionId);
        access.CompanyId.Should().Be(company.Id);
        access.Visibility.Should().Be(ConnectionVisibility.Company);

        // The new owner can now read the connection.
        (await setup.InviteeClient.GetAsync($"/api/connections/{connectionId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Transferring_a_dashboard_requires_a_company()
    {
        var client = CreateClient();
        var email = $"dashnocompany_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        var otherClient = CreateClient();
        var otherEmail = $"dashother_{Guid.NewGuid():N}@example.com";
        await otherClient.RegisterAndLoginAsync(otherEmail);
        var otherId = await _factory.GetUserIdAsync(otherEmail);

        var dashboardId = await CreateDashboardAsync(client, "Solo board");

        var transfer = await client.PostAsJsonAsync(
            $"/api/dashboards/{dashboardId}/transfer-ownership",
            new TransferDashboardRequest(otherId, ApiClientExtensions.DefaultPassword));
        transfer.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(HttpClient OwnerClient, Guid OwnerId, HttpClient InviteeClient, Guid InviteeId)> CreateCompanyWithMemberAsync()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"dashcoowner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);

        var companyResp = await ownerClient.PostAsJsonAsync("/api/companies", new CreateCompanyRequest($"DashCo-{Guid.NewGuid():N}"));
        companyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var company = await companyResp.ReadJsonAsync<CompanyResponse>();
        await _factory.SeedCompanySubscriptionAsync(company.Id);

        var roleResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/roles",
            new CreateRoleRequest("Member", false, false, false, false));
        roleResp.EnsureSuccessStatusCode();
        var role = await roleResp.ReadJsonAsync<CompanyRoleResponse>();

        var inviteeEmail = $"dashcoinvitee_{Guid.NewGuid():N}@example.com";
        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id));
        inviteResp.EnsureSuccessStatusCode();

        var invites = await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>();
        var pending = invites.First(i => i.Email == inviteeEmail && !i.IsAccepted);

        var inviteeClient = CreateClient();
        await inviteeClient.RegisterAndLoginAsync(inviteeEmail);
        var accept = await inviteeClient.PostAsJsonAsync("/api/companies/accept-invite", new AcceptInviteRequest(pending.Id));
        accept.EnsureSuccessStatusCode();

        var ownerId = await _factory.GetUserIdAsync(ownerEmail);
        var inviteeId = await _factory.GetUserIdAsync(inviteeEmail);
        return (ownerClient, ownerId, inviteeClient, inviteeId);
    }
}
