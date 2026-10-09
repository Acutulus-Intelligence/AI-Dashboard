using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class TwoFactorAuthRoutesTests
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string Password = ApiClientExtensions.DefaultPassword;

    private readonly ApiFactory _factory;

    public TwoFactorAuthRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Setup_enable_login_recovery_and_challenge_token_flow()
    {
        var client = CreateClient();
        var email = $"tfa_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        // Enabling revokes all sessions, forcing a fresh 2FA login.
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Codes are hashed at rest and unused.
        var recoveryRows = await GetRecoveryRowsAsync(email);
        recoveryRows.Should().HaveCount(10);
        recoveryRows.Should().OnlyContain(r => r.UsedAt == null && r.CodeHash.Length > 0);

        // Password login alone must not authenticate a 2FA user.
        var login = await client.LoginAsync(email);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Headers.Contains("Set-Cookie").Should().BeFalse();

        var loginBody = await login.ReadJsonAsync<AuthResponse>();
        loginBody.RequiresTwoFactor.Should().BeTrue();
        loginBody.ChallengeToken.Should().NotBeNullOrEmpty();

        // The pending challenge is not usable as an access token.
        var bearer = CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new("Bearer", loginBody.ChallengeToken);
        (await bearer.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Wrong code is rejected.
        var badChallenge = await GetChallengeAsync(client, email);
        var wrong = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(badChallenge, WrongCode(CurrentCode(sharedKey)), false));
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Correct TOTP code completes login.
        await LoginWithTotpAsync(client, email, sharedKey);
        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.ReadJsonAsync<UserMeResponse>()).TwoFactorEnabled.Should().BeTrue();

        // Regenerating recovery codes requires the correct password.
        var wrongRegenerate = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
            new RegenerateRecoveryCodesRequest(CurrentCode(sharedKey), "WrongPass123!!"));
        wrongRegenerate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var regenerate = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
            new RegenerateRecoveryCodesRequest(CurrentCode(sharedKey), Password));
        regenerate.StatusCode.Should().Be(HttpStatusCode.OK);
        var regenerated = await regenerate.ReadJsonAsync<TwoFactorRecoveryCodesResponse>();
        regenerated.RecoveryCodes.Should().HaveCount(10);
        var recoveryCode = regenerated.RecoveryCodes[0];

        // A recovery code works exactly once.
        (await client.PostAsync("/api/auth/revoke", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rcChallenge = await GetChallengeAsync(client, email);
        var redeem = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(rcChallenge, recoveryCode, true));
        redeem.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetRecoveryRowsAsync(email)).Count(r => r.UsedAt == null).Should().Be(9);

        (await client.PostAsync("/api/auth/revoke", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reuseChallenge = await GetChallengeAsync(client, email);
        var reused = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(reuseChallenge, recoveryCode, true));
        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Setup_requires_correct_password()
    {
        var client = CreateClient();
        var email = $"tfa_setup_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var setup = await client.PostAsJsonAsync("/api/auth/2fa/setup",
            new SetupTwoFactorRequest("WrongPass123!!"));
        setup.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var me = await client.GetAsync("/api/auth/me");
        (await me.ReadJsonAsync<UserMeResponse>()).TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Enable_with_invalid_code_is_rejected()
    {
        var client = CreateClient();
        var email = $"tfa_bad_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var setup = await client.PostAsJsonAsync("/api/auth/2fa/setup", new SetupTwoFactorRequest(Password));
        setup.StatusCode.Should().Be(HttpStatusCode.OK);
        var setupBody = await setup.ReadJsonAsync<TwoFactorSetupResponse>();

        var enable = await client.PostAsJsonAsync("/api/auth/2fa/enable",
            new EnableTwoFactorRequest(WrongCode(CurrentCode(setupBody.SharedKey))));
        enable.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var me = await client.GetAsync("/api/auth/me");
        (await me.ReadJsonAsync<UserMeResponse>()).TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Adjacent_time_step_is_accepted_but_far_code_is_rejected()
    {
        var client = CreateClient();
        var email = $"tfa_skew_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var setup = await client.PostAsJsonAsync("/api/auth/2fa/setup", new SetupTwoFactorRequest(Password));
        var setupBody = await setup.ReadJsonAsync<TwoFactorSetupResponse>();

        // Well outside the accepted window (framework allows +/-2 steps).
        var far = await client.PostAsJsonAsync("/api/auth/2fa/enable",
            new EnableTwoFactorRequest(Totp(setupBody.SharedKey, -3)));
        far.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // One step of drift is tolerated.
        var adjacent = await client.PostAsJsonAsync("/api/auth/2fa/enable",
            new EnableTwoFactorRequest(Totp(setupBody.SharedKey, -1)));
        adjacent.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Disable_requires_valid_code_and_password()
    {
        var client = CreateClient();
        var email = $"tfa_off_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        // Enabling revoked the session; sign in again with 2FA before disabling.
        await LoginWithTotpAsync(client, email, sharedKey);

        var badCode = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest(WrongCode(CurrentCode(sharedKey)), Password));
        badCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var badPassword = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest(CurrentCode(sharedKey), "WrongPass123!!"));
        badPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var disable = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest(CurrentCode(sharedKey), Password));
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Security stamp rotation invalidates the current session.
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await GetRecoveryRowsAsync(email)).Should().BeEmpty();

        var relogin = CreateClient();
        (await relogin.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await relogin.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Regenerate_recovery_codes_requires_password()
    {
        var client = CreateClient();
        var email = $"tfa_regen_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        var wrong = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
            new RegenerateRecoveryCodesRequest(CurrentCode(sharedKey), "WrongPass123!!"));
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var ok = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
            new RegenerateRecoveryCodesRequest(CurrentCode(sharedKey), Password));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Password_change_revokes_all_sessions()
    {
        var client = CreateClient();
        var email = $"tfa_pw_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(Password, "NewPass123!!", "NewPass123!!"));
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var relogin = CreateClient();
        (await relogin.LoginAsync(email, "NewPass123!!")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Account_locks_after_five_failed_password_attempts()
    {
        var email = $"tfa_lock_pw_{Guid.NewGuid():N}@example.com";
        (await CreateClient().RegisterAsync(email)).EnsureSuccessStatusCode();

        var loginClient = CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var attempt = await loginClient.LoginAsync(email, "WrongPass123!!");
            attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // Account is now locked; even the correct password is refused.
        var locked = await loginClient.LoginAsync(email, Password);
        locked.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task Account_locks_after_five_failed_2fa_attempts()
    {
        var email = $"tfa_lock_code_{Guid.NewGuid():N}@example.com";
        var client = CreateClient();
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        for (var i = 0; i < 5; i++)
        {
            var challenge = await GetChallengeAsync(client, email);
            var attempt = await client.PostAsJsonAsync("/api/auth/login/2fa",
                new TwoFactorLoginRequest(challenge, WrongCode(CurrentCode(sharedKey)), false));
            attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // Account is locked after the 5th failed code; password login is refused.
        var locked = await client.LoginAsync(email);
        locked.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task Company_invite_preserves_two_factor()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"tfa_owner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);

        var create = await ownerClient.PostAsJsonAsync("/api/companies", new CreateCompanyRequest($"Co-{Guid.NewGuid():N}"));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var company = await create.ReadJsonAsync<CompanyResponse>();

        var roleResp = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/roles",
            new CreateRoleRequest("Member", true, false, false, false));
        roleResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var role = await roleResp.ReadJsonAsync<CompanyRoleResponse>();

        var memberEmail = $"tfa_member_{Guid.NewGuid():N}@example.com";
        var memberClient = CreateClient();
        await memberClient.RegisterAndLoginAsync(memberEmail);
        var (sharedKey, _) = await EnableTwoFactorAsync(memberClient);
        await LoginWithTotpAsync(memberClient, memberEmail, sharedKey);

        var invite = await ownerClient.PostAsJsonAsync($"/api/companies/{company.Id}/invite",
            new InviteUserRequest(memberEmail, role.Id));
        invite.StatusCode.Should().Be(HttpStatusCode.OK);

        var invites = await ownerClient.GetAsync($"/api/companies/{company.Id}/invites");
        var inviteList = await invites.ReadJsonAsync<List<CompanyInviteResponse>>();
        var pending = inviteList.First(i => i.Email == memberEmail && !i.IsAccepted);

        var accept = await memberClient.PostAsJsonAsync("/api/companies/accept-invite",
            new AcceptInviteRequest(pending.Id));
        accept.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 2FA survives the individual -> company transition.
        var me = await memberClient.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await me.ReadJsonAsync<UserMeResponse>();
        meBody.TwoFactorEnabled.Should().BeTrue();
        meBody.UserType.Should().Be(UserType.Company);

        // A fresh sign-in still requires the second factor.
        var fresh = CreateClient();
        var login = await fresh.LoginAsync(memberEmail);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await login.ReadJsonAsync<AuthResponse>()).RequiresTwoFactor.Should().BeTrue();
    }

    private static async Task<(string SharedKey, IReadOnlyList<string> RecoveryCodes)> EnableTwoFactorAsync(
        HttpClient client, string password = Password)
    {
        var setup = await client.PostAsJsonAsync("/api/auth/2fa/setup", new SetupTwoFactorRequest(password));
        setup.StatusCode.Should().Be(HttpStatusCode.OK);
        var setupBody = await setup.ReadJsonAsync<TwoFactorSetupResponse>();

        setupBody.SharedKey.Should().NotBeNullOrEmpty();
        setupBody.AuthenticatorUri.Should().StartWith("otpauth://totp/");

        var enable = await client.PostAsJsonAsync("/api/auth/2fa/enable",
            new EnableTwoFactorRequest(CurrentCode(setupBody.SharedKey)));
        enable.StatusCode.Should().Be(HttpStatusCode.OK);
        var enableBody = await enable.ReadJsonAsync<TwoFactorRecoveryCodesResponse>();
        enableBody.RecoveryCodes.Should().HaveCount(10);

        return (setupBody.SharedKey, enableBody.RecoveryCodes);
    }

    private static async Task<string> GetChallengeAsync(HttpClient client, string email)
    {
        var login = await client.LoginAsync(email);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.ReadJsonAsync<AuthResponse>();
        body.RequiresTwoFactor.Should().BeTrue();
        return body.ChallengeToken!;
    }

    private static async Task LoginWithTotpAsync(HttpClient client, string email, string sharedKey)
    {
        var challenge = await GetChallengeAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(challenge, CurrentCode(sharedKey), false));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<List<Domain.Models.TwoFactorRecoveryCode>> GetRecoveryRowsAsync(string email)
    {
        var userId = await _factory.GetUserIdAsync(email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TwoFactorRecoveryCodes
            .Where(c => c.UserId == userId)
            .ToListAsync();
    }

    private static string CurrentCode(string sharedKey) => Totp(sharedKey, 0);

    private static string WrongCode(string correct)
    {
        var replacement = ((correct[^1] - '0') + 1) % 10;
        return correct[..^1] + replacement;
    }

    private static string Totp(string base32Key, long timeStepOffset)
    {
        var key = Base32Decode(base32Key);
        var timeStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + timeStepOffset;

        var data = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(data);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(data);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        var cleaned = input.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;

        foreach (var c in cleaned)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
                continue;

            buffer = (buffer << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }
}
