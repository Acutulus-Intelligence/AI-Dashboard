using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Application.DTos.Request;
using Application.DTos.Response;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class TwoFactorAuthRoutesTests
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

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
        var goodChallenge = await GetChallengeAsync(client, email);
        var good = await client.PostAsJsonAsync("/api/auth/login/2fa",
            new TwoFactorLoginRequest(goodChallenge, CurrentCode(sharedKey), false));
        good.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await me.ReadJsonAsync<UserMeResponse>()).TwoFactorEnabled.Should().BeTrue();

        // A recovery code works exactly once (regeneration invalidates the previous set).
        var regenerate = await client.PostAsJsonAsync("/api/auth/2fa/recovery-codes",
            new RegenerateRecoveryCodesRequest(CurrentCode(sharedKey)));
        regenerate.StatusCode.Should().Be(HttpStatusCode.OK);
        var regenerated = await regenerate.ReadJsonAsync<TwoFactorRecoveryCodesResponse>();
        regenerated.RecoveryCodes.Should().HaveCount(10);
        var recoveryCode = regenerated.RecoveryCodes[0];

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
    public async Task Enable_with_invalid_code_is_rejected()
    {
        var client = CreateClient();
        var email = $"tfa_bad_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var setup = await client.PostAsync("/api/auth/2fa/setup", null);
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

        var setup = await client.PostAsync("/api/auth/2fa/setup", null);
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
    public async Task Disable_requires_valid_code_and_revokes_sessions()
    {
        var client = CreateClient();
        var email = $"tfa_off_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var (sharedKey, _) = await EnableTwoFactorAsync(client);

        // Enabling revoked the session; sign in again with 2FA before disabling.
        await LoginWithTotpAsync(client, email, sharedKey);

        var badDisable = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest(WrongCode(CurrentCode(sharedKey))));
        badDisable.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var disable = await client.PostAsJsonAsync("/api/auth/2fa/disable",
            new DisableTwoFactorRequest(CurrentCode(sharedKey)));
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Security stamp rotation invalidates the current session.
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await GetRecoveryRowsAsync(email)).Should().BeEmpty();

        var relogin = CreateClient();
        (await relogin.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await relogin.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<(string SharedKey, IReadOnlyList<string> RecoveryCodes)> EnableTwoFactorAsync(HttpClient client)
    {
        var setup = await client.PostAsync("/api/auth/2fa/setup", null);
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
