using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Enums;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class InvitationRoutesTests
{
    private readonly ApiFactory _factory;

    public InvitationRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    private static readonly Regex InviteLinkRegex = new(@"/invite/([A-Za-z0-9_\-]+)", RegexOptions.Compiled);

    private async Task<(HttpClient Owner, CompanyResponse Company, CompanyRoleResponse Role, string OwnerEmail)>
        SetupCompanyAsync()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"owner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);

        var create = await ownerClient.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest($"Co-{Guid.NewGuid():N}"));
        create.EnsureSuccessStatusCode();
        var company = await create.ReadJsonAsync<CompanyResponse>();

        var createRole = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/roles",
            new CreateRoleRequest("Analyst", true, false, false, true));
        createRole.EnsureSuccessStatusCode();
        var role = await createRole.ReadJsonAsync<CompanyRoleResponse>();

        return (ownerClient, company, role, ownerEmail);
    }

    private static string ExtractToken(Application.Interfaces.EmailMessage message)
    {
        var body = message.TextBody ?? message.HtmlBody;
        var match = InviteLinkRegex.Match(body);
        match.Success.Should().BeTrue($"invitation email should contain an invite link, body was: {body}");
        return match.Groups[1].Value;
    }

    private Application.Interfaces.EmailMessage LatestInviteEmail(string recipient)
    {
        var messages = _factory.FakeEmails.MessagesTo(recipient);
        messages.Should().NotBeEmpty();
        return messages.Last();
    }

    [Fact]
    public async Task Email_invitation_sends_link_and_accepting_creates_member()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        invite.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = ExtractToken(LatestInviteEmail(inviteeEmail));

        var anon = CreateClient();
        var detailsResp = await anon.GetAsync($"/api/invitations/{token}");
        detailsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var details = await detailsResp.ReadJsonAsync<InvitationDetailsResponse>();
        details.Email.Should().Be(inviteeEmail);
        details.CompanyName.Should().Be(company.Name);
        details.RoleName.Should().Be("Analyst");
        details.HasAccount.Should().BeFalse();

        var accept = await anon.PostAsJsonAsync($"/api/invitations/{token}/accept",
            new AcceptEmailInvitationRequest("New", "Member", ApiClientExtensions.DefaultPassword));
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await anon.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var roleId = await _factory.GetUserCompanyRoleIdAsync(inviteeEmail);
        roleId.Should().Be(role.Id);
    }

    [Fact]
    public async Task Invitation_token_is_single_use_and_deleted_after_acceptance()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();

        var token = ExtractToken(LatestInviteEmail(inviteeEmail));

        var anon = CreateClient();
        var accept = await anon.PostAsJsonAsync($"/api/invitations/{token}/accept",
            new AcceptEmailInvitationRequest("New", "Member", ApiClientExtensions.DefaultPassword));
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-validating the consumed token must fail.
        var second = CreateClient();
        var validateAgain = await second.GetAsync($"/api/invitations/{token}");
        validateAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var reaccept = await second.PostAsJsonAsync($"/api/invitations/{token}/accept",
            new AcceptEmailInvitationRequest("New", "Member", ApiClientExtensions.DefaultPassword));
        reaccept.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_pending_invite_is_rejected()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var first = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Resend_rotates_token_and_sends_new_email()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();
        var firstToken = ExtractToken(LatestInviteEmail(inviteeEmail));

        var invites = await ownerClient.GetAsync($"/api/companies/{company.Id}/invites");
        var invite = (await invites.ReadJsonAsync<List<CompanyInviteResponse>>())
            .First(i => i.Email == inviteeEmail);

        var resend = await ownerClient.PostAsync($"/api/companies/{company.Id}/invites/{invite.Id}/resend", null);
        resend.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondToken = ExtractToken(LatestInviteEmail(inviteeEmail));
        secondToken.Should().NotBe(firstToken);

        var anon = CreateClient();
        (await anon.GetAsync($"/api/invitations/{firstToken}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anon.GetAsync($"/api/invitations/{secondToken}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Internal_invite_returns_token_and_sends_no_email()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Internal));
        invite.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await invite.Content.ReadFromJsonAsync<InviteTokenResponse>(ApiClientExtensions.JsonOptions);
        body!.Token.Should().NotBeNullOrEmpty();

        _factory.FakeEmails.MessagesTo(inviteeEmail).Should().BeEmpty();
    }

    [Fact]
    public async Task Invite_requires_authentication()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync($"/api/companies/{Guid.NewGuid()}/invite",
            new InviteUserRequest("x@example.com", Guid.NewGuid(), InviteDeliveryMethod.Email));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Failed_email_invite_does_not_persist_and_can_be_retried()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        _factory.FakeEmails.FailNextSend = true;
        var failed = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        failed.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // The failed send must not leave a pending invite behind.
        var afterFailure = await ownerClient.GetAsync($"/api/companies/{company.Id}/invites");
        var invitesAfterFailure = await afterFailure.ReadJsonAsync<List<CompanyInviteResponse>>();
        invitesAfterFailure.Should().NotContain(i => i.Email == inviteeEmail);
        _factory.FakeEmails.MessagesTo(inviteeEmail).Should().BeEmpty();

        // Retrying succeeds because the orphaned invite was rolled back.
        var retry = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        retry.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterRetry = await ownerClient.GetAsync($"/api/companies/{company.Id}/invites");
        var invitesAfterRetry = await afterRetry.ReadJsonAsync<List<CompanyInviteResponse>>();
        invitesAfterRetry.Should().Contain(i => i.Email == inviteeEmail);
        _factory.FakeEmails.MessagesTo(inviteeEmail).Should().HaveCount(1);
    }

    [Fact]
    public async Task Failed_resend_keeps_previous_link_valid()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();
        var originalToken = ExtractToken(LatestInviteEmail(inviteeEmail));

        var invites = await ownerClient.GetAsync($"/api/companies/{company.Id}/invites");
        var invite = (await invites.ReadJsonAsync<List<CompanyInviteResponse>>())
            .First(i => i.Email == inviteeEmail);

        _factory.FakeEmails.FailNextSend = true;
        var resend = await ownerClient.PostAsync($"/api/companies/{company.Id}/invites/{invite.Id}/resend", null);
        resend.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // The original link must still work after a failed resend.
        var anon = CreateClient();
        (await anon.GetAsync($"/api/invitations/{originalToken}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record InviteTokenResponse(string? Token);
}
