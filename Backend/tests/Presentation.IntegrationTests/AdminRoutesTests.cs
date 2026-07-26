using System.Net;
using System.Net.Http.Json;
using Application.Dtos.Request;
using Application.Dtos.Response;
using Domain.Enums;
using FluentAssertions;

namespace Presentation.IntegrationTests;

[Collection("Api")]
public sealed class AdminRoutesTests
{
    private readonly ApiFactory _factory;

    public AdminRoutesTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task Admin_subscription_plan_crud_works()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);

        // Re-login so JWT includes Admin role
        await client.LoginAsync(email);

        var list = await client.GetAsync("/api/admin/subscription-plans");
        list.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await create.ReadJsonAsync<SubscriptionPlanResponse>();

        var byId = await client.GetAsync($"/api/admin/subscription-plans/{plan.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{plan.Id}",
            new UpdateSubscriptionPlanRequest(
                plan.Name, "Updated", UserType.Individual, 19.99m, 199.99m, null, null, null, true));
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/admin/subscription-plans/{plan.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Non_admin_gets_forbidden()
    {
        var client = CreateClient();
        var email = $"user_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var response = await client.GetAsync("/api/admin/subscription-plans");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_gets_unauthorized()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/admin/subscription-plans");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
