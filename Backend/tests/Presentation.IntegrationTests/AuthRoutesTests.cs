using System.Net;
using System.Net.Http.Json;
using Application.DTos.Request;
using Application.DTos.Response;
using Domain.Models;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class AuthRoutesTests
{
    private readonly ApiFactory _factory;

    public AuthRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Register_Login_Me_Refresh_Revoke_Profile_Password_work()
    {
        var client = CreateClient();
        var email = $"auth_{Guid.NewGuid():N}@example.com";

        var register = await client.RegisterAsync(email);
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await me.Content.ReadAsStringAsync();
        meBody.Should().Contain(email);

        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await client.PutAsJsonAsync("/api/auth/profile", new UpdateProfileRequest("Ada", "Lovelace", null));
        profile.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var changePassword = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(ApiClientExtensions.DefaultPassword, "NewPass123!!", "NewPass123!!"));
        changePassword.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await client.LoginAsync(email, "NewPass123!!");
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var revoke = await client.PostAsync("/api/auth/revoke", null);
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfterRevoke = await client.GetAsync("/api/auth/me");
        meAfterRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_account_works()
    {
        var client = CreateClient();
        var email = $"del_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(ApiClientExtensions.DefaultPassword)),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_account_soft_deactivates_and_can_reactivate()
    {
        var client = CreateClient();
        var email = $"delsoft_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(ApiClientExtensions.DefaultPassword)),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (isDeactivated, scheduledAt) = await _factory.GetUserDeletionStateAsync(email);
        isDeactivated.Should().BeTrue();
        scheduledAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(14), TimeSpan.FromMinutes(1));

        // The current session was ended.
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A deactivated account may still sign in during the grace period.
        var relogin = CreateClient();
        (await relogin.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await relogin.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await me.ReadJsonAsync<UserMeResponse>();
        meBody.IsDeactivated.Should().BeTrue();
        meBody.DeletionScheduledAt.Should().NotBeNull();

        // Reactivation requires the password.
        var wrong = await relogin.PostAsJsonAsync("/api/auth/account/reactivate",
            new ReactivateAccountRequest("WrongPass123!!"));
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var reactivate = await relogin.PostAsJsonAsync("/api/auth/account/reactivate",
            new ReactivateAccountRequest(ApiClientExtensions.DefaultPassword));
        reactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _factory.GetUserDeletionStateAsync(email)).IsDeactivated.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_account_with_wrong_password_is_rejected()
    {
        var client = CreateClient();
        var email = $"delwrong_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("WrongPass123!!")),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _factory.GetUserDeletionStateAsync(email)).IsDeactivated.Should().BeFalse();
    }

    [Fact]
    public async Task Reactivation_after_grace_period_is_rejected()
    {
        var client = CreateClient();
        var email = $"react_expired_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        await _factory.SetUserDeletionScheduledAtAsync(email, DateTime.UtcNow.AddDays(-1));

        var reactivate = await client.PostAsJsonAsync("/api/auth/account/reactivate",
            new ReactivateAccountRequest(ApiClientExtensions.DefaultPassword));
        reactivate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Changing_email_revokes_all_sessions()
    {
        var client = CreateClient();
        var email = $"emailchg_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        var newEmail = $"emailchg_new_{Guid.NewGuid():N}@example.com";

        var profile = await client.PutAsJsonAsync("/api/auth/profile",
            new UpdateProfileRequest("Ada", "Lovelace", newEmail));
        profile.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var relogin = CreateClient();
        (await relogin.LoginAsync(newEmail)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reusing_a_refresh_token_revokes_all_sessions()
    {
        var client = CreateClient();
        var email = $"refresh_reuse_{Guid.NewGuid():N}@example.com";

        var register = await client.RegisterAsync(email);
        register.EnsureSuccessStatusCode();
        var firstRefreshToken = ReadRefreshCookie(register);

        // Rotate once; the client now holds a newer refresh token.
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Replaying the original token is detected...
        var replay = CreateClient();
        replay.DefaultRequestHeaders.Add("Cookie", $"refresh_token={firstRefreshToken}");
        (await replay.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // ...and revokes every session, including the rotated token.
        (await client.PostAsync("/api/auth/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_without_auth_returns_401()
    {
        var client = CreateClient();
        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rotation_links_old_token_to_its_successor()
    {
        var client = CreateClient();
        var email = $"rotlink_{Guid.NewGuid():N}@example.com";

        var register = await client.RegisterAsync(email);
        register.EnsureSuccessStatusCode();

        var refresh = await client.PostAsync("/api/auth/refresh", null);
        refresh.EnsureSuccessStatusCode();

        var userId = await _factory.GetUserIdAsync(email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.Set<RefreshToken>().AsNoTracking()
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        rows.Should().HaveCount(2);

        var oldRow = rows.Single(rt => rt.IsUsed);
        var newRow = rows.Single(rt => !rt.IsUsed);

        // The replaced token points at its successor; the successor is only
        // linked once it is itself rotated.
        oldRow.ReplacedByToken.Should().Be(newRow.Token);
        newRow.ReplacedByToken.Should().BeNull();
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("refresh_token=", StringComparison.Ordinal))
            : null;
        setCookie.Should().NotBeNull();

        var value = setCookie!["refresh_token=".Length..];
        return value[..value.IndexOf(';')];
    }
}
