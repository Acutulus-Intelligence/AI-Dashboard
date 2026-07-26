using System.Net;
using System.Net.Http.Json;
using Application.Dtos.Request;
using FluentAssertions;

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
    public async Task Me_without_auth_returns_401()
    {
        var client = CreateClient();
        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
