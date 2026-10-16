using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
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

        // The login above consumed the current time step, so a fresh code is required.
        var regenerate = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
            new RegenerateRecoveryCodesRequest(Totp(sharedKey, 1), Password));
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
    public async Task Enable_when_already_enabled_is_rejected()
    {
        var client = CreateClient();
        var email = $"tfa_double_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        var enableAgain = await client.PostAsJsonAsync("/api/auth/2fa/enable",
            new EnableTwoFactorRequest(CurrentCode(sharedKey)));
        enableAgain.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Setup_when_already_enabled_is_rejected()
    {
        var client = CreateClient();
        var email = $"tfa_resetup_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        var setupAgain = await client.PostAsJsonAsync("/api/auth/2fa/setup",
            new SetupTwoFactorRequest(Password));
        setupAgain.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The account is still protected by 2FA after the rejected setup attempt.
        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.ReadJsonAsync<UserMeResponse>()).TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Authenticator_key_is_encrypted_at_rest()
    {
        var client = CreateClient();
        var email = $"tfa_enc_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        var tokens = await GetUserTokensAsync(email);
        var keyToken = tokens.First(t => t.Name == "AuthenticatorKey");
        keyToken.Value.Should().StartWith("v2:");
        keyToken.Value.Should().NotBe(sharedKey);
    }

    [Fact]
    public async Task Totp_code_cannot_be_replayed_within_window()
    {
        var client = CreateClient();
        var email = $"tfa_replay_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        // Capture a single code and use it once successfully.
        var code = CurrentCode(sharedKey);
        var firstChallenge = await GetChallengeAsync(client, email);
        var first = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(firstChallenge, code, false));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // The exact same code must be refused on a fresh challenge.
        var secondChallenge = await GetChallengeAsync(client, email);
        var replay = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(secondChallenge, code, false));
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The rejection explains that the code was already used.
        var problem = await replay.ReadJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problem.Detail.Should().Contain("already been used");
    }

    [Fact]
    public async Task Sensitive_operation_cannot_replay_the_login_code()
    {
        var client = CreateClient();
        var email = $"tfa_replay_op_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        var code = CurrentCode(sharedKey);
        var challenge = await GetChallengeAsync(client, email);
        var login = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(challenge, code, false));
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        // The same code must not be redeemable again for a sensitive operation.
        var regenerate = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
            new RegenerateRecoveryCodesRequest(code, Password));
        regenerate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await regenerate.ReadJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problem.Detail.Should().Contain("already been used");
    }

    [Fact]
    public async Task Challenge_token_cannot_be_reused_after_success()
    {
        var client = CreateClient();
        var email = $"tfa_chal_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        var challenge = await GetChallengeAsync(client, email);
        var first = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(challenge, CurrentCode(sharedKey), false));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reusing the same challenge (even with a fresh code) must fail.
        var reuse = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(challenge, CurrentCode(sharedKey), false));
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Disable_with_recovery_code_succeeds()
    {
        var client = CreateClient();
        var email = $"tfa_rcoff_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, recoveryCodes) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        var disable = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest(Password, null, recoveryCodes[0]));
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetRecoveryRowsAsync(email)).Should().BeEmpty();
    }

    [Fact]
    public async Task Enable_cancels_setup_after_three_failed_codes_without_locking_account()
    {
        var client = CreateClient();
        var email = $"tfa_lock_enable_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var setup = await client.PostAsJsonAsync("/api/auth/2fa/setup", new SetupTwoFactorRequest(Password));
        setup.StatusCode.Should().Be(HttpStatusCode.OK);
        var setupBody = await setup.ReadJsonAsync<TwoFactorSetupResponse>();

        // First two wrong codes are plain rejections.
        for (var i = 0; i < 2; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/auth/2fa/enable",
                new EnableTwoFactorRequest(WrongCode(CurrentCode(setupBody.SharedKey))));
            attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // The third wrong code cancels the pending setup rather than locking the account.
        var third = await client.PostAsJsonAsync("/api/auth/2fa/enable",
            new EnableTwoFactorRequest(WrongCode(CurrentCode(setupBody.SharedKey))));
        third.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The staged key is gone, so enabling immediately fails.
        var afterCancel = await client.PostAsJsonAsync("/api/auth/2fa/enable",
            new EnableTwoFactorRequest(CurrentCode(setupBody.SharedKey)));
        afterCancel.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Failed setup must not lock sign-in.
        var relogin = CreateClient();
        (await relogin.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Restarting setup grants a fresh set of attempts and succeeds.
        var restart = await client.PostAsJsonAsync("/api/auth/2fa/setup", new SetupTwoFactorRequest(Password));
        restart.StatusCode.Should().Be(HttpStatusCode.OK);
        var restartBody = await restart.ReadJsonAsync<TwoFactorSetupResponse>();
        var enable = await client.PostAsJsonAsync("/api/auth/2fa/enable",
            new EnableTwoFactorRequest(CurrentCode(restartBody.SharedKey)));
        enable.StatusCode.Should().Be(HttpStatusCode.OK);
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
            new DisableTwoFactorRequest(Password, WrongCode(CurrentCode(sharedKey))));
        badCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var badPassword = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest("WrongPass123!!", CurrentCode(sharedKey)));
        badPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The login above consumed the current time step, so a fresh code is required.
        var disable = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest(Password, Totp(sharedKey, 1)));
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

        // The login above consumed the current time step, so a fresh code is required.
        var ok = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
            new RegenerateRecoveryCodesRequest(Totp(sharedKey, 1), Password));
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
    public async Task Correct_password_resets_password_failures_but_two_factor_failures_track_separately()
    {
        var client = CreateClient();
        var email = $"tfa_split_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        // Accumulate password failures...
        for (var i = 0; i < 3; i++)
        {
            (await client.LoginAsync(email, "WrongPass123!!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        (await _factory.GetAccessFailedCountAsync(email)).Should().Be(3);

        // ...a correct password clears them.
        var challenge = await GetChallengeAsync(client, email);
        (await _factory.GetAccessFailedCountAsync(email)).Should().Be(0);

        // Second-factor failures are tracked on their own counter and do not
        // touch the password counter.
        for (var i = 0; i < 2; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/auth/login/2fa",
                new TwoFactorLoginRequest(challenge, WrongCode(CurrentCode(sharedKey)), false));
            attempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await _factory.GetTwoFactorFailedAttemptsAsync(email)).Should().Be(2);
        (await _factory.GetAccessFailedCountAsync(email)).Should().Be(0);
    }

    [Fact]
    public async Task Failed_two_factor_disable_attempts_do_not_lock_account()
    {
        var client = CreateClient();
        var email = $"tfa_disable_lock_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        // Well past the login lockout threshold: wrong password and wrong code.
        for (var i = 0; i < 5; i++)
        {
            var badPassword = await client.PostAsJsonAsync("/api/auth/2fa/disable",
                new DisableTwoFactorRequest("WrongPass123!!", CurrentCode(sharedKey)));
            badPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var badCode = await client.PostAsJsonAsync("/api/auth/2fa/disable",
                new DisableTwoFactorRequest(Password, WrongCode(CurrentCode(sharedKey))));
            badCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // Managing 2FA must not feed the sign-in lockout.
        (await _factory.GetAccessFailedCountAsync(email)).Should().Be(0);
        var relogin = CreateClient();
        (await relogin.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Failed_recovery_code_regeneration_does_not_lock_account()
    {
        var client = CreateClient();
        var email = $"tfa_regen_lock_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        for (var i = 0; i < 5; i++)
        {
            var badPassword = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
                new RegenerateRecoveryCodesRequest(CurrentCode(sharedKey), "WrongPass123!!"));
            badPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var badCode = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
                new RegenerateRecoveryCodesRequest(WrongCode(CurrentCode(sharedKey)), Password));
            badCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await _factory.GetAccessFailedCountAsync(email)).Should().Be(0);
        var relogin = CreateClient();
        (await relogin.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Setup_is_refused_while_account_is_locked()
    {
        var client = CreateClient();
        var email = $"tfa_setup_lock_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        await _factory.SetUserLockoutAsync(email, DateTimeOffset.UtcNow.AddMinutes(5));

        var setup = await client.PostAsJsonAsync("/api/auth/2fa/setup", new SetupTwoFactorRequest(Password));
        setup.StatusCode.Should().Be(HttpStatusCode.Locked);
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

    [Fact]
    public async Task Delete_account_requires_two_factor_code_when_enabled()
    {
        var client = CreateClient();
        var email = $"tfa_del_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        // A missing or wrong second factor is rejected.
        var missing = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(Password)),
        });
        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var wrong = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(Password, WrongCode(CurrentCode(sharedKey)))),
        });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _factory.GetUserDeletionStateAsync(email)).IsDeactivated.Should().BeFalse();

        // A correct code deactivates the account (fresh step; login consumed the current one).
        var ok = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(Password, Totp(sharedKey, 1))),
        });
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _factory.GetUserDeletionStateAsync(email)).IsDeactivated.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_account_accepts_recovery_code_when_two_factor_enabled()
    {
        var client = CreateClient();
        var email = $"tfa_delrc_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, recoveryCodes) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        var ok = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(Password, null, recoveryCodes[0])),
        });
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _factory.GetUserDeletionStateAsync(email)).IsDeactivated.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_account_requires_password_and_two_factor()
    {
        var client = CreateClient();
        var email = $"tfa_react_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);
        await LoginWithTotpAsync(client, email, sharedKey);

        // Simulate a scheduled deletion while keeping the current session valid so
        // the 2FA-required reactivation path can be exercised in isolation.
        await _factory.SetUserDeletionScheduledAtAsync(email, DateTime.UtcNow.AddDays(14));

        // The second factor is required.
        var missing = await client.PostAsJsonAsync("/api/auth/account/reactivate",
            new ReactivateAccountRequest(Password));
        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/account/reactivate",
            new ReactivateAccountRequest("WrongPass123!!", CurrentCode(sharedKey)));
        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var wrongCode = await client.PostAsJsonAsync("/api/auth/account/reactivate",
            new ReactivateAccountRequest(Password, WrongCode(CurrentCode(sharedKey))));
        wrongCode.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The login above consumed the current time step, so a fresh code is required.
        var ok = await client.PostAsJsonAsync("/api/auth/account/reactivate",
            new ReactivateAccountRequest(Password, Totp(sharedKey, 1)));
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _factory.GetUserDeletionStateAsync(email)).IsDeactivated.Should().BeFalse();
    }

    [Fact]
    public async Task Purge_removes_accounts_past_the_grace_period()
    {
        var client = CreateClient();
        var email = $"purge_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(Password)),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var userId = await _factory.GetUserIdAsync(email);

        // A private data collection created by the user is a required (Restrict)
        // reference; the purge must remove it so the user can be deleted.
        var collectionId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.DataCollections.Add(new DataCollection
            {
                Id = collectionId,
                Name = "Purge me",
                CreatedById = userId,
                Visibility = CollectionVisibility.Private,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await _factory.SetUserDeletionScheduledAtAsync(email, DateTime.UtcNow.AddDays(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var deletionService = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            var purged = await deletionService.PurgeExpiredAccountsAsync();
            purged.Should().BeGreaterThanOrEqualTo(1);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await users.FindByIdAsync(userId.ToString())).Should().BeNull();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.DataCollections.FindAsync(collectionId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task Purge_reassigns_company_collections_to_the_owner()
    {
        var ownerClient = CreateClient();
        var ownerEmail = $"purgecoowner_{Guid.NewGuid():N}@example.com";
        await ownerClient.RegisterAndLoginAsync(ownerEmail);

        var companyResp = await ownerClient.PostAsJsonAsync(
            "/api/companies", new CreateCompanyRequest($"PurgeCo-{Guid.NewGuid():N}"));
        companyResp.EnsureSuccessStatusCode();
        var company = await companyResp.ReadJsonAsync<CompanyResponse>();
        await _factory.SeedCompanySubscriptionAsync(company.Id);

        var roleResp = await ownerClient.PostAsJsonAsync(
            $"/api/companies/{company.Id}/roles", new CreateRoleRequest("Member", false, false, false, false));
        roleResp.EnsureSuccessStatusCode();
        var role = await roleResp.ReadJsonAsync<CompanyRoleResponse>();

        var memberEmail = $"purgecomember_{Guid.NewGuid():N}@example.com";
        var inviteResp = await ownerClient.PostAsJsonAsync(
            $"/api/companies/{company.Id}/invite", new InviteUserRequest(memberEmail, role.Id));
        inviteResp.EnsureSuccessStatusCode();
        var invites = await (await ownerClient.GetAsync($"/api/companies/{company.Id}/invites"))
            .ReadJsonAsync<List<CompanyInviteResponse>>();
        var pending = invites.First(i => i.Email == memberEmail && !i.IsAccepted);

        var memberClient = CreateClient();
        await memberClient.RegisterAndLoginAsync(memberEmail);
        var accept = await memberClient.PostAsJsonAsync(
            "/api/companies/accept-invite", new AcceptInviteRequest(pending.Id));
        accept.EnsureSuccessStatusCode();

        var ownerId = await _factory.GetUserIdAsync(ownerEmail);
        var memberId = await _factory.GetUserIdAsync(memberEmail);

        var collectionId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.DataCollections.Add(new DataCollection
            {
                Id = collectionId,
                Name = "Team data",
                CompanyId = company.Id,
                CreatedById = memberId,
                Visibility = CollectionVisibility.Company,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await _factory.SetUserDeletionScheduledAtAsync(memberEmail, DateTime.UtcNow.AddDays(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var deletionService = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            await deletionService.PurgeExpiredAccountsAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await users.FindByIdAsync(memberId.ToString())).Should().BeNull();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var collection = await db.DataCollections.FindAsync(collectionId);
            collection.Should().NotBeNull();
            collection!.CreatedById.Should().Be(ownerId);
        }
    }

    [Fact]
    public async Task Purge_removes_chart_referenced_by_another_users_widget()
    {
        var client = CreateClient();
        var email = $"purgechart_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);

        var otherClient = CreateClient();
        var otherEmail = $"purgeother_{Guid.NewGuid():N}@example.com";
        await otherClient.RegisterAndLoginAsync(otherEmail);
        var otherId = await _factory.GetUserIdAsync(otherEmail);

        var chartId = Guid.NewGuid();
        var dashboardId = Guid.NewGuid();
        var widgetId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SavedCharts.Add(new SavedChart
            {
                Id = chartId,
                UserId = userId,
                Title = "Referenced chart",
                ChartType = "bar",
                XAxis = "category",
                YAxis = ["amount"],
                Aggregation = "sum",
                SqlQuery = "SELECT 1",
            });
            db.Dashboards.Add(new Dashboard
            {
                Id = dashboardId,
                Name = "Other board",
                UserId = otherId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.DashboardWidgets.Add(new DashboardWidget
            {
                Id = widgetId,
                DashboardId = dashboardId,
                WidgetType = WidgetType.Chart,
                SavedChartId = chartId,
                Width = 6,
                Height = 4,
            });
            await db.SaveChangesAsync();
        }

        await _factory.SetUserDeletionScheduledAtAsync(email, DateTime.UtcNow.AddDays(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var deletionService = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            await deletionService.PurgeExpiredAccountsAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await users.FindByIdAsync(userId.ToString())).Should().BeNull();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var widget = await db.DashboardWidgets.FindAsync(widgetId);
            widget.Should().NotBeNull();
            widget!.SavedChartId.Should().BeNull();
            (await db.Dashboards.FindAsync(dashboardId)).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Purge_deletes_account_even_when_stripe_cancellation_fails()
    {
        var client = CreateClient();
        var email = $"purgestripe_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var userId = await _factory.GetUserIdAsync(email);
        await _factory.SeedActiveSubscriptionAsync(email);

        string? stripeSubscriptionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            stripeSubscriptionId = (await db.UserSubscriptions
                .FirstAsync(s => s.UserId == userId)).StripeSubscriptionId;
        }

        stripeSubscriptionId.Should().NotBeNull();
        _factory.FakePayments.ImmediateCancelFailures.Add(stripeSubscriptionId!);

        await _factory.SetUserDeletionScheduledAtAsync(email, DateTime.UtcNow.AddDays(-1));

        using (var scope = _factory.Services.CreateScope())
        {
            var deletionService = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            await deletionService.PurgeExpiredAccountsAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await users.FindByIdAsync(userId.ToString())).Should().BeNull();
        }
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

    private async Task<List<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>> GetUserTokensAsync(string email)
    {
        var userId = await _factory.GetUserIdAsync(email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.UserTokens
            .Where(t => t.UserId == userId)
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
