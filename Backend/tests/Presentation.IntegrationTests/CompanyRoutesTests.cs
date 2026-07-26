using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Dtos.Request;
using Application.Dtos.Response;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class CompanyRoutesTests
{
    private readonly ApiFactory _factory;

    public CompanyRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Company_routes_cover_create_members_roles_invites_and_delete()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"owner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);

        var create = await ownerClient.PostAsJsonAsync("/api/companies", new CreateCompanyRequest($"Co-{Guid.NewGuid():N}"));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var company = await create.ReadJsonAsync<CompanyResponse>();

        var me = await ownerClient.GetAsync("/api/companies/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var byId = await ownerClient.GetAsync($"/api/companies/{company.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await ownerClient.GetAsync($"/api/companies/{company.Id}/users");
        users.StatusCode.Should().Be(HttpStatusCode.OK);

        var rolesResp = await ownerClient.GetAsync($"/api/companies/{company.Id}/roles");
        rolesResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var createRole = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/roles",
            new CreateRoleRequest("Analyst", true, false, false, true));
        createRole.StatusCode.Should().Be(HttpStatusCode.OK);
        var newRole = await createRole.ReadJsonAsync<CompanyRoleResponse>();

        var updateRole = await ownerClient.PutAsJsonAsync($"/api/companies/{company.Id}/roles/{newRole.Id}",
            new UpdateRoleRequest("Analyst+", true, false, false, true));
        updateRole.StatusCode.Should().Be(HttpStatusCode.OK);

        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";
        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, newRole.Id));
        invite.StatusCode.Should().Be(HttpStatusCode.OK);

        var invites = await ownerClient.GetAsync($"/api/companies/{company.Id}/invites");
        invites.StatusCode.Should().Be(HttpStatusCode.OK);
        var inviteList = await invites.ReadJsonAsync<List<CompanyInviteResponse>>();
        var pendingInvite = inviteList.First(i => i.Email == inviteeEmail && !i.IsAccepted);

        var inviteeClient = CreateClient();
        await inviteeClient.RegisterAndLoginAsync(inviteeEmail);

        var pending = await inviteeClient.GetAsync("/api/invites/pending");
        pending.StatusCode.Should().Be(HttpStatusCode.OK);

        var accept = await inviteeClient.PostAsJsonAsync("/api/companies/accept-invite",
            new AcceptInviteRequest(pendingInvite.Id));
        accept.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var inviteeId = await _factory.GetUserIdAsync(inviteeEmail);
        var updateUserRole = await ownerClient.PutAsJsonAsync(
            $"/api/companies/{company.Id}/users/{inviteeId}/role",
            new UpdateUserRoleRequest(newRole.Id));
        updateUserRole.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Second invitee for transfer + reject flows
        var otherEmail = $"other_{Guid.NewGuid():N}@example.com";
        var invite2 = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(otherEmail, newRole.Id));
        invite2.StatusCode.Should().Be(HttpStatusCode.OK);

        var invites2 = await ownerClient.GetAsync($"/api/companies/{company.Id}/invites");
        var inviteList2 = await invites2.ReadJsonAsync<List<CompanyInviteResponse>>();
        var otherInvite = inviteList2.First(i => i.Email == otherEmail && !i.IsAccepted);

        var otherClient = CreateClient();
        await otherClient.RegisterAndLoginAsync(otherEmail);

        var reject = await otherClient.DeleteAsync($"/api/invites/{otherInvite.Id}");
        reject.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var thirdEmail = $"third_{Guid.NewGuid():N}@example.com";
        var invite3 = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(thirdEmail, newRole.Id));
        invite3.EnsureSuccessStatusCode();
        var invites3 = await ownerClient.GetAsync($"/api/companies/{company.Id}/invites");
        var inviteList3 = await invites3.ReadJsonAsync<List<CompanyInviteResponse>>();
        var thirdInvite = inviteList3.First(i => i.Email == thirdEmail && !i.IsAccepted);

        var revoke = await ownerClient.DeleteAsync($"/api/companies/{company.Id}/invites/{thirdInvite.Id}");
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var transfer = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/transfer-ownership",
            new TransferOwnershipRequest(inviteeId, ApiClientExtensions.DefaultPassword));
        transfer.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Owner is now invitee — remove former owner, then delete company
        var formerOwnerId = await _factory.GetUserIdAsync(ownerEmail);
        var remove = await inviteeClient.DeleteAsync($"/api/companies/{company.Id}/users/{formerOwnerId}");
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteCompany = await inviteeClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/companies/{company.Id}")
        {
            Content = JsonContent.Create(new DeleteCompanyRequest(ApiClientExtensions.DefaultPassword)),
        });
        deleteCompany.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_unused_role_works()
    {
        var client = CreateClient();
        var email = $"role_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var companyResp = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest($"RoleCo-{Guid.NewGuid():N}"));
        var company = await companyResp.ReadJsonAsync<CompanyResponse>();

        var createRole = await client.PostAsJsonAsync($"/api/companies/{company.Id}/roles",
            new CreateRoleRequest("TempRole", false, false, false, false));
        var role = await createRole.ReadJsonAsync<CompanyRoleResponse>();

        var deleteRole = await client.DeleteAsync($"/api/companies/{company.Id}/roles/{role.Id}");
        deleteRole.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Company_without_auth_returns_401()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/companies/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
