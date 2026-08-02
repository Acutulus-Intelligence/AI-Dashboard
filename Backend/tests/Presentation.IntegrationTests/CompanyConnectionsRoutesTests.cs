using System.Net;
using System.Net.Http.Json;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Enums;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class CompanyConnectionsRoutesTests
{
    private readonly ApiFactory _factory;

    public CompanyConnectionsRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    private async Task<CompanyResponse> CreateCompanyAsync(HttpClient ownerClient)
    {
        var resp = await ownerClient.PostAsJsonAsync("/api/companies", new CreateCompanyRequest($"Co-{Guid.NewGuid():N}"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return await resp.ReadJsonAsync<CompanyResponse>();
    }

    private async Task<CompanyRoleResponse> CreateRoleAsync(
        HttpClient ownerClient, Guid companyId, string name, bool canManageConnections)
    {
        var resp = await ownerClient.PostAsJsonAsync($"/api/companies/{companyId}/roles",
            new CreateRoleRequest(name, false, false, false, false, canManageConnections));
        resp.EnsureSuccessStatusCode();
        return await resp.ReadJsonAsync<CompanyRoleResponse>();
    }

    private async Task<HttpClient> JoinCompanyAsync(
        HttpClient ownerClient, Guid companyId, Guid roleId, string email)
    {
        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{companyId}/invite",
            new InviteUserRequest(email, roleId));
        invite.EnsureSuccessStatusCode();

        var invites = await (await ownerClient.GetAsync($"/api/companies/{companyId}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>();
        var pending = invites.First(i => i.Email == email && !i.IsAccepted);

        var memberClient = CreateClient();
        await memberClient.RegisterAndLoginAsync(email);
        var accept = await memberClient.PostAsJsonAsync("/api/companies/accept-invite",
            new AcceptInviteRequest(pending.Id));
        accept.EnsureSuccessStatusCode();
        return memberClient;
    }

    private CreateConnectionRequest Conn(string name, ConnectionVisibility visibility, List<Guid>? roles = null) =>
        new(name, DbProvider.PostgreSql, _factory.ExternalHost, _factory.ExternalPort,
            _factory.ExternalDatabase, _factory.ExternalUsername, _factory.ExternalPassword,
            visibility, roles);

    [Fact]
    public async Task Company_connections_follow_visibility_and_permission_rules()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"cowner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);
        var company = await CreateCompanyAsync(ownerClient);
        await _factory.SeedCompanySubscriptionAsync(company.Id);

        var analystRole = await CreateRoleAsync(ownerClient, company.Id, "Analyst", canManageConnections: false);
        var dbAdminRole = await CreateRoleAsync(ownerClient, company.Id, "DbAdmin", canManageConnections: true);
        var viewerRole = await CreateRoleAsync(ownerClient, company.Id, "Viewer", canManageConnections: false);

        var analystClient = await JoinCompanyAsync(ownerClient, company.Id, analystRole.Id, $"analyst_{Guid.NewGuid():N}@example.com");
        var adminClient = await JoinCompanyAsync(ownerClient, company.Id, dbAdminRole.Id, $"dbadmin_{Guid.NewGuid():N}@example.com");
        var viewerClient = await JoinCompanyAsync(ownerClient, company.Id, viewerRole.Id, $"viewer_{Guid.NewGuid():N}@example.com");

        // Owner creates a company-shared connection.
        var sharedResp = await ownerClient.PostAsJsonAsync("/api/connections", Conn("Company Shared", ConnectionVisibility.Company));
        sharedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var shared = await sharedResp.ReadJsonAsync<ConnectionResponse>();
        shared.CompanyId.Should().Be(company.Id);
        shared.Visibility.Should().Be(ConnectionVisibility.Company);

        // Owner creates a role-only connection for the Analyst role.
        var rolesOnlyResp = await ownerClient.PostAsJsonAsync("/api/connections",
            Conn("Analyst Only", ConnectionVisibility.Roles, new List<Guid> { analystRole.Id }));
        rolesOnlyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rolesOnly = await rolesOnlyResp.ReadJsonAsync<ConnectionResponse>();

        // Analyst (no manage) can view the shared connection and browse its tables.
        var analystList = await (await analystClient.GetAsync("/api/connections")).ReadJsonAsync<List<ConnectionResponse>>();
        analystList.Should().Contain(c => c.Id == shared.Id);
        analystList.Should().Contain(c => c.Id == rolesOnly.Id);
        (await analystClient.GetAsync($"/api/connections/{shared.Id}/tables")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Viewer (no manage, not in the allowed role) sees only the company-shared connection.
        var viewerList = await (await viewerClient.GetAsync("/api/connections")).ReadJsonAsync<List<ConnectionResponse>>();
        viewerList.Should().Contain(c => c.Id == shared.Id);
        viewerList.Should().NotContain(c => c.Id == rolesOnly.Id);
        (await viewerClient.GetAsync($"/api/connections/{rolesOnly.Id}/tables")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Analyst cannot create, edit, or delete connections.
        var denyCreate = await analystClient.PostAsJsonAsync("/api/connections", Conn("Denied", ConnectionVisibility.Company));
        denyCreate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var denyEdit = await analystClient.PutAsJsonAsync($"/api/connections/{shared.Id}",
            new UpdateConnectionRequest("Hacked", DbProvider.PostgreSql, _factory.ExternalHost, _factory.ExternalPort,
                _factory.ExternalDatabase, _factory.ExternalUsername, _factory.ExternalPassword,
                ConnectionVisibility.Company, null));
        denyEdit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await analystClient.DeleteAsync($"/api/connections/{shared.Id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // DbAdmin (CanManageConnections) sees every shared connection.
        var adminList = await (await adminClient.GetAsync("/api/connections")).ReadJsonAsync<List<ConnectionResponse>>();
        adminList.Should().Contain(c => c.Id == shared.Id);
        adminList.Should().Contain(c => c.Id == rolesOnly.Id);

        // DbAdmin can create, fetch config for, and edit connections.
        var adminConnResp = await adminClient.PostAsJsonAsync("/api/connections", Conn("DbAdmin Conn", ConnectionVisibility.Company));
        adminConnResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminConn = await adminConnResp.ReadJsonAsync<ConnectionResponse>();

        (await adminClient.GetAsync($"/api/connections/{adminConn.Id}/config")).StatusCode.Should().Be(HttpStatusCode.OK);

        var editResp = await adminClient.PutAsJsonAsync($"/api/connections/{adminConn.Id}",
            new UpdateConnectionRequest("DbAdmin Updated", DbProvider.PostgreSql, _factory.ExternalHost, _factory.ExternalPort,
                _factory.ExternalDatabase, _factory.ExternalUsername, null,
                ConnectionVisibility.Company, null));
        editResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // A private connection created by the owner is invisible and unmanageable for managers.
        var ownerPrivateResp = await ownerClient.PostAsJsonAsync("/api/connections", Conn("Owner Private", ConnectionVisibility.Private));
        ownerPrivateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerPrivate = await ownerPrivateResp.ReadJsonAsync<ConnectionResponse>();

        var adminList2 = await (await adminClient.GetAsync("/api/connections")).ReadJsonAsync<List<ConnectionResponse>>();
        adminList2.Should().NotContain(c => c.Id == ownerPrivate.Id);

        var denyPrivateEdit = await adminClient.PutAsJsonAsync($"/api/connections/{ownerPrivate.Id}",
            new UpdateConnectionRequest("Hack", DbProvider.PostgreSql, _factory.ExternalHost, _factory.ExternalPort,
                _factory.ExternalDatabase, _factory.ExternalUsername, null,
                ConnectionVisibility.Company, null));
        denyPrivateEdit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Cleanup via the owner.
        (await ownerClient.DeleteAsync($"/api/connections/{shared.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ownerClient.DeleteAsync($"/api/connections/{rolesOnly.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Company_connections_hit_five_connection_limit()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"limit_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);
        var company = await CreateCompanyAsync(ownerClient);
        await _factory.SeedCompanySubscriptionAsync(company.Id);

        for (var i = 1; i <= 5; i++)
        {
            var resp = await ownerClient.PostAsJsonAsync("/api/connections", Conn($"Conn {i}", ConnectionVisibility.Company));
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var over = await ownerClient.PostAsJsonAsync("/api/connections", Conn("Sixth", ConnectionVisibility.Company));
        over.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Owner_is_the_only_one_who_can_grant_connection_management()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"grant_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);
        var company = await CreateCompanyAsync(ownerClient);

        // Admin (CanManageRoles but not owner) cannot create a role granting connection management.
        var adminRole = await CreateRoleAsync(ownerClient, company.Id, "Admin", canManageConnections: false);
        var updateResp = await ownerClient.PutAsJsonAsync($"/api/companies/{company.Id}/roles/{adminRole.Id}",
            new UpdateRoleRequest("Admin", true, true, true, true, false));
        updateResp.EnsureSuccessStatusCode();
        var updatedAdmin = await updateResp.ReadJsonAsync<CompanyRoleResponse>();
        updatedAdmin.CanManageRoles.Should().BeTrue();

        var adminClient = await JoinCompanyAsync(ownerClient, company.Id, adminRole.Id, $"grantadmin_{Guid.NewGuid():N}@example.com");

        var denyGrant = await adminClient.PostAsJsonAsync($"/api/companies/{company.Id}/roles",
            new CreateRoleRequest("Power", false, false, false, false, CanManageConnections: true));
        denyGrant.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_owner_can_edit_role_that_already_has_connection_management()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"edit_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);
        var company = await CreateCompanyAsync(ownerClient);

        // Owner grants connection management on a role.
        var dbAdminRole = await CreateRoleAsync(ownerClient, company.Id, "DbAdmin", canManageConnections: true);

        // Manager role can manage roles but cannot grant connection management.
        var managerRole = await CreateRoleAsync(ownerClient, company.Id, "Manager", canManageConnections: false);
        var updateManager = await ownerClient.PutAsJsonAsync($"/api/companies/{company.Id}/roles/{managerRole.Id}",
            new UpdateRoleRequest("Manager", false, false, true, false, false));
        updateManager.EnsureSuccessStatusCode();

        var managerClient = await JoinCompanyAsync(ownerClient, company.Id, managerRole.Id, $"mgr_{Guid.NewGuid():N}@example.com");

        // Manager can edit the DbAdmin role (name + tables) even though it carries connection management.
        var edit = await managerClient.PutAsJsonAsync($"/api/companies/{company.Id}/roles/{dbAdminRole.Id}",
            new UpdateRoleRequest("DbAdmin2", false, false, false, false, true, ["Orders"]));
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        var edited = await edit.ReadJsonAsync<CompanyRoleResponse>();
        edited.Name.Should().Be("DbAdmin2");
        edited.CanManageConnections.Should().BeTrue();

        // But manager still cannot grant connection management to a role that lacks it.
        var analystRole = await CreateRoleAsync(ownerClient, company.Id, "Analyst", canManageConnections: false);
        var denyGrant = await managerClient.PutAsJsonAsync($"/api/companies/{company.Id}/roles/{analystRole.Id}",
            new UpdateRoleRequest("Analyst", false, false, false, false, true));
        denyGrant.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
