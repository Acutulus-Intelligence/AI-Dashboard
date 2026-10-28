using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Enums;
using Domain.Models;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class InvitationRoutesTests
{
    private readonly ApiFactory _factory;

    public InvitationRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    private static readonly Regex InviteLinkRegex = new(@"/invite#([A-Za-z0-9_\-]+)", RegexOptions.Compiled);

    private async Task<(HttpClient Owner, CompanyResponse Company, CompanyRoleResponse Role, string OwnerEmail)>
        SetupCompanyAsync(bool withSubscription = true)
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"owner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);

        var create = await ownerClient.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest($"Co-{Guid.NewGuid():N}"));
        create.EnsureSuccessStatusCode();
        var company = await create.ReadJsonAsync<CompanyResponse>();

        if (withSubscription)
            await _factory.SeedCompanySubscriptionAsync(company.Id);

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

    private static Task<HttpResponseMessage> LookupAsync(HttpClient client, string token)
        => client.PostAsJsonAsync("/api/invitations/lookup", new LookupInvitationRequest(token));

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient client, string token)
        => client.PostAsJsonAsync("/api/invitations/accept",
            new AcceptInvitationRequest(token, "New", "Member", ApiClientExtensions.DefaultPassword));

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    [Fact]
    public async Task Email_invitation_sends_link_and_accepting_creates_member()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        invite.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var token = ExtractToken(LatestInviteEmail(inviteeEmail));

        var anon = CreateClient();
        var detailsResp = await LookupAsync(anon, token);
        detailsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var details = await detailsResp.ReadJsonAsync<InvitationDetailsResponse>();
        details.Email.Should().Be(inviteeEmail);
        details.CompanyName.Should().Be(company.Name);
        details.RoleName.Should().Be("Analyst");
        details.HasAccount.Should().BeFalse();

        var accept = await AcceptAsync(anon, token);
        accept.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await anon.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        var roleId = await _factory.GetUserCompanyRoleIdAsync(inviteeEmail);
        roleId.Should().Be(role.Id);
    }

    [Fact]
    public async Task Invitation_token_is_single_use_after_acceptance()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();

        var token = ExtractToken(LatestInviteEmail(inviteeEmail));

        var anon = CreateClient();
        (await AcceptAsync(anon, token)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-validating the consumed token must fail.
        var second = CreateClient();
        (await LookupAsync(second, token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AcceptAsync(second, token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_pending_invite_is_rejected()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var first = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

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
        (await LookupAsync(anon, firstToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LookupAsync(anon, secondToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Internal_invite_sends_no_email()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Internal));
        invite.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _factory.FakeEmails.MessagesTo(inviteeEmail).Should().BeEmpty();
    }

    [Fact]
    public async Task Email_invitation_escapes_company_name_in_body()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        // Simulate legacy data created before the validator rejected markup.
        await _factory.SetCompanyNameAsync(company.Id,
            "Acme <a href=\"https://evil.example\">Verify</a> & co");

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        invite.EnsureSuccessStatusCode();

        var message = LatestInviteEmail(inviteeEmail);
        message.HtmlBody.Should().Contain("&lt;a href=");
        message.HtmlBody.Should().NotContain("<a href=\"https://evil.example\">");
    }

    [Fact]
    public async Task Email_invite_requires_active_company_subscription()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync(withSubscription: false);
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));

        invite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorCodeAsync(invite)).Should().Be("subscription_required");
        _factory.FakeEmails.MessagesTo(inviteeEmail).Should().BeEmpty();
    }

    [Fact]
    public async Task Invite_is_rejected_when_user_limit_reached()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync(withSubscription: false);
        // Owner already occupies the single seat.
        await _factory.SeedCompanySubscriptionAsync(company.Id, maxUsers: 1);
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));

        invite.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(invite)).Should().Be("max_users_reached");
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
        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);

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
        (await LookupAsync(anon, originalToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoked_invitation_cannot_be_redeemed()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();
        var token = ExtractToken(LatestInviteEmail(inviteeEmail));

        var invites = await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>();
        var invite = invites.First(i => i.Email == inviteeEmail && !i.IsAccepted);
        (await ownerClient.DeleteAsync($"/api/companies/{company.Id}/invites/{invite.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anon = CreateClient();
        (await LookupAsync(anon, token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AcceptAsync(anon, token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Expired_invitation_cannot_be_redeemed()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();
        var token = ExtractToken(LatestInviteEmail(inviteeEmail));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invite = await db.CompanyInvites.FirstAsync(i => i.Email == inviteeEmail && !i.IsAccepted);
            invite.ExpiresAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        var anon = CreateClient();
        (await LookupAsync(anon, token)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AcceptAsync(anon, token)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_with_invite_token_for_wrong_email_leaves_no_account()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";
        var wrongEmail = $"wrong_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        invite.EnsureSuccessStatusCode();
        var token = ExtractToken(LatestInviteEmail(inviteeEmail));

        var client = CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            wrongEmail, ApiClientExtensions.DefaultPassword, "Wrong", "User", UserType.Company, token));
        register.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The rejected registration must not have created an account, and the
        // invite must still be redeemable by its intended recipient.
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await users.FindByEmailAsync(wrongEmail)).Should().BeNull();
        }

        var anon = CreateClient();
        (await LookupAsync(anon, token)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Invite_to_user_in_another_company_is_rejected()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();

        // Another company owner already belongs to a different company, so an
        // invite from this company could never be accepted.
        var otherClient = CreateClient();
        var otherEmail = $"other_{Guid.NewGuid():N}@example.com";
        await otherClient.RegisterAndLoginAsync(otherEmail);
        var otherCompany = await (await otherClient.PostAsJsonAsync(
            "/api/companies", new CreateCompanyRequest($"Other-{Guid.NewGuid():N}")))
            .ReadJsonAsync<CompanyResponse>();
        otherCompany.Should().NotBeNull();

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(otherEmail, role.Id, InviteDeliveryMethod.Email));

        invite.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(invite)).Should().Be("already_in_company");
        _factory.FakeEmails.MessagesTo(otherEmail).Should().BeEmpty();
    }

    [Fact]
    public async Task Resend_requires_active_company_subscription()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();

        var invite = (await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>())
            .First(i => i.Email == inviteeEmail && !i.IsAccepted);

        // Lapse the company's subscription, then try to resend.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscriptions = await db.CompanySubscriptions
                .Where(s => s.CompanyId == company.Id)
                .ToListAsync();
            db.CompanySubscriptions.RemoveRange(subscriptions);
            await db.SaveChangesAsync();
        }

        var resend = await ownerClient.PostAsync(
            $"/api/companies/{company.Id}/invites/{invite.Id}/resend", null);

        resend.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadErrorCodeAsync(resend)).Should().Be("subscription_required");
        _factory.FakeEmails.MessagesTo(inviteeEmail).Should().HaveCount(1);
    }

    [Fact]
    public async Task Deleting_role_with_pending_invite_succeeds()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        invite.EnsureSuccessStatusCode();

        // CompanyInvite.Role is a Restrict FK; deleting the role must clear the
        // reference instead of failing with a database error.
        var delete = await ownerClient.DeleteAsync($"/api/companies/{company.Id}/roles/{role.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var invites = await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>();
        invites.Should().NotContain(i => i.RoleId == role.Id);
    }

    [Fact]
    public async Task Deactivated_account_cannot_create_company()
    {
        var client = CreateClient();
        var email = $"deactivated_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        // Simulate an account inside its deletion grace period (still signed in).
        await _factory.SetUserDeletionScheduledAtAsync(email, DateTime.UtcNow.AddDays(5));

        var create = await client.PostAsJsonAsync("/api/companies",
            new CreateCompanyRequest($"Ghost-{Guid.NewGuid():N}"));

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_role_preserves_accepted_invite_history()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Internal));
        invite.EnsureSuccessStatusCode();

        var invites = await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>();
        var pending = invites.First(i => i.Email == inviteeEmail && !i.IsAccepted);

        var inviteeClient = CreateClient();
        await inviteeClient.RegisterAndLoginAsync(inviteeEmail);
        (await inviteeClient.PostAsJsonAsync("/api/companies/accept-invite",
            new AcceptInviteRequest(pending.Id))).EnsureSuccessStatusCode();

        var delete = await ownerClient.DeleteAsync($"/api/companies/{company.Id}/roles/{role.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>();
        var accepted = after.Single(i => i.Email == inviteeEmail);

        accepted.IsAccepted.Should().BeTrue();
        accepted.RoleId.Should().BeNull();
        accepted.RoleName.Should().Be("Analyst");
    }

    [Fact]
    public async Task Resend_counts_toward_daily_email_cap()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();

        var invite = (await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>())
            .First(i => i.Email == inviteeEmail && !i.IsAccepted);

        await SeedEmailSendsAsync(company.Id, 49);

        var resend = await ownerClient.PostAsync(
            $"/api/companies/{company.Id}/invites/{invite.Id}/resend", null);

        resend.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(resend)).Should().Be("email_invite_rate_limited");
        _factory.FakeEmails.MessagesTo(inviteeEmail).Should().HaveCount(1);
    }

    [Fact]
    public async Task Revoked_invite_still_counts_toward_daily_email_cap()
    {
        var (ownerClient, company, role, _) = await SetupCompanyAsync();
        var inviteeEmail = $"invitee_{Guid.NewGuid():N}@example.com";

        var inviteResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(inviteeEmail, role.Id, InviteDeliveryMethod.Email));
        inviteResp.EnsureSuccessStatusCode();

        var invite = (await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>())
            .First(i => i.Email == inviteeEmail && !i.IsAccepted);

        (await ownerClient.DeleteAsync($"/api/companies/{company.Id}/invites/{invite.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await SeedEmailSendsAsync(company.Id, 49);

        var secondEmail = $"second_{Guid.NewGuid():N}@example.com";
        var second = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(secondEmail, role.Id, InviteDeliveryMethod.Email));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadErrorCodeAsync(second)).Should().Be("email_invite_rate_limited");
        _factory.FakeEmails.MessagesTo(secondEmail).Should().BeEmpty();
    }

    private async Task SeedEmailSendsAsync(Guid companyId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.CompanyInviteEmailSends.AddRange(Enumerable.Range(0, count).Select(i => new CompanyInviteEmailSend
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            RecipientEmail = $"seed_{i}@example.com",
            SentAt = DateTime.UtcNow
        }));

        await db.SaveChangesAsync();
    }
}
