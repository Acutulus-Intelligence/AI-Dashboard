using System.Net;
using System.Net.Http.Json;
using Application.DTos.Request;
using Application.DTos.Response;
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
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();
        plan.StripeProductId.Should().NotBeNullOrEmpty();
        plan.StripeMonthlyPriceId.Should().NotBeNullOrEmpty();
        plan.StripeYearlyPriceId.Should().NotBeNullOrEmpty();

        var byId = await client.GetAsync($"/api/admin/subscription-plans/{plan.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{plan.Id}",
            new UpdateSubscriptionPlanRequest(
                plan.Name, "Updated", UserType.Individual, 19.99m, 199.99m, null, null, null, true));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedPlan = await update.ReadJsonAsync<AdminSubscriptionPlanResponse>();
        updatedPlan.StripeProductId.Should().NotBeNullOrEmpty();
        updatedPlan.MonthlyPrice.Should().Be(19.99m);

        var deactivate = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{plan.Id}",
            new UpdateSubscriptionPlanRequest(
                updatedPlan.Name, updatedPlan.Description, UserType.Individual, 19.99m, 199.99m, null, null, null, false));
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/admin/subscription-plans/{plan.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Admin_cannot_delete_active_plan()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var delete = await client.DeleteAsync($"/api/admin/subscription-plans/{plan.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_remove_deactivated_plan_without_canceling_existing_subscription()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var userEmail = $"user_{Guid.NewGuid():N}@example.com";
        await client.RegisterAsync(userEmail);
        await client.LoginAsync(email);
        var userId = await _factory.GetUserIdAsync(userEmail);
        await _factory.SeedSubscriptionForPlanAsync(userId, plan.Id);

        var deactivate = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{plan.Id}",
            new UpdateSubscriptionPlanRequest(
                plan.Name, plan.Description, UserType.Individual, 9.99m, 99.99m, null, null, null, false));
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);

        var remove = await client.DeleteAsync($"/api/admin/subscription-plans/{plan.Id}");
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.VerifySubscriptionStillActiveAsync(userId);
    }

    [Fact]
    public async Task Admin_can_move_subscriptions_to_another_plan()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var createA = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"PlanA-{Guid.NewGuid():N}", "Test plan A", UserType.Individual, 9.99m, 99.99m, null, null, null));
        createA.StatusCode.Should().Be(HttpStatusCode.OK);
        var planA = await createA.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var createB = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"PlanB-{Guid.NewGuid():N}", "Test plan B", UserType.Individual, 19.99m, 199.99m, null, null, null));
        createB.StatusCode.Should().Be(HttpStatusCode.OK);
        var planB = await createB.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var userEmail = $"user_{Guid.NewGuid():N}@example.com";
        await client.RegisterAsync(userEmail);
        await client.LoginAsync(email);
        var userId = await _factory.GetUserIdAsync(userEmail);
        await _factory.SeedSubscriptionForPlanAsync(userId, planA.Id);

        var deactivateA = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{planA.Id}",
            new UpdateSubscriptionPlanRequest(
                planA.Name, planA.Description, UserType.Individual, 9.99m, 99.99m, null, null, null, false));
        deactivateA.StatusCode.Should().Be(HttpStatusCode.OK);

        var move = await client.PostAsJsonAsync($"/api/admin/subscription-plans/{planA.Id}/move",
            new MoveSubscriptionPlanRequest(planB.Id));
        move.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.VerifySubscriptionOnPlanAsync(userId, planB.Id, 19.99m);
    }

    [Fact]
    public async Task Admin_can_list_users_and_promote_admin_role()
    {
        var client = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var targetEmail = $"target_{Guid.NewGuid():N}@example.com";

        await client.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureAdminRoleAsync(actorEmail);
        await client.LoginAsync(actorEmail);

        (await client.RegisterAsync(targetEmail)).EnsureSuccessStatusCode();
        await client.LoginAsync(actorEmail);
        var targetId = await _factory.GetUserIdAsync(targetEmail);

        var list = await client.GetAsync("/api/admin/users");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await list.ReadJsonAsync<List<AdminUserResponse>>();
        users.Should().Contain(u => u.Email == targetEmail);

        var promote = await client.PutAsJsonAsync($"/api/admin/users/{targetId}/admin-role",
            new UpdateAdminRoleRequest(true));
        promote.StatusCode.Should().Be(HttpStatusCode.OK);
        var promoted = await promote.ReadJsonAsync<AdminUserResponse>();
        promoted.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_cannot_revoke_another_admin()
    {
        var client = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var otherEmail = $"admin_{Guid.NewGuid():N}@example.com";

        await client.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureAdminRoleAsync(actorEmail);
        await client.LoginAsync(actorEmail);

        (await client.RegisterAsync(otherEmail)).EnsureSuccessStatusCode();
        await _factory.EnsureAdminRoleAsync(otherEmail);
        await client.LoginAsync(actorEmail);
        var otherId = await _factory.GetUserIdAsync(otherEmail);

        var revoke = await client.PutAsJsonAsync($"/api/admin/users/{otherId}/admin-role",
            new UpdateAdminRoleRequest(false));
        revoke.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_cannot_revoke_own_admin_role()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var actorId = await _factory.GetUserIdAsync(email);
        var response = await client.PutAsJsonAsync($"/api/admin/users/{actorId}/admin-role",
            new UpdateAdminRoleRequest(false));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_view_account_stats()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var response = await client.GetAsync("/api/admin/stats");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.ReadJsonAsync<AdminStatsResponse>();
        stats.TotalUsers.Should().BeGreaterThanOrEqualTo(1);
        stats.IndividualSubscribedUsers.Should().BeGreaterThanOrEqualTo(0);
        stats.CompanySubscribedUsers.Should().BeGreaterThanOrEqualTo(0);
        stats.UsersWithoutSubscription.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Admin_can_create_user_with_admin_role()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"newadmin_{Guid.NewGuid():N}@example.com", "TestPass123!!", "New", "Admin", UserType.Individual, "Admin"));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await create.ReadJsonAsync<AdminUserResponse>();
        user.IsAdmin.Should().BeTrue();
        user.Roles.Should().Contain("Admin");
    }

    [Fact]
    public async Task Admin_can_create_user_with_moderator_role()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"newmod_{Guid.NewGuid():N}@example.com", "TestPass123!!", "New", "Mod", UserType.Individual, "Moderator"));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await create.ReadJsonAsync<AdminUserResponse>();
        user.IsAdmin.Should().BeFalse();
        user.IsModerator.Should().BeTrue();
        user.Roles.Should().Contain("Moderator");
    }

    [Fact]
    public async Task Admin_cannot_create_staff_account_with_company_type()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var createAdmin = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"staff_{Guid.NewGuid():N}@example.com", "TestPass123!!", "Staff", "Admin", UserType.Company, "Admin"));
        createAdmin.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var createMod = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"staff_{Guid.NewGuid():N}@example.com", "TestPass123!!", "Staff", "Mod", UserType.Company, "Moderator"));
        createMod.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_cannot_create_user_with_existing_email()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var existingEmail = $"existing_{Guid.NewGuid():N}@example.com";
        (await client.RegisterAsync(existingEmail)).EnsureSuccessStatusCode();

        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest(existingEmail, "TestPass123!!", "Dup", "User", UserType.Individual, "User"));
        create.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_can_remove_moderator_role()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureAdminRoleAsync(email);
        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"mod_{Guid.NewGuid():N}@example.com", "TestPass123!!", "Temp", "Mod", UserType.Individual, "Moderator"));
        create.EnsureSuccessStatusCode();
        var mod = await create.ReadJsonAsync<AdminUserResponse>();

        var remove = await client.PutAsJsonAsync($"/api/admin/users/{mod.Id}/moderator-role",
            new UpdateModeratorRoleRequest(false));
        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        var removed = await remove.ReadJsonAsync<AdminUserResponse>();
        removed.IsModerator.Should().BeFalse();
    }

    [Fact]
    public async Task Admin_can_transfer_admin_role_to_moderator()
    {
        var client = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureAdminRoleAsync(actorEmail);
        await client.LoginAsync(actorEmail);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"mod_{Guid.NewGuid():N}@example.com", "TestPass123!!", "Next", "Admin", UserType.Individual, "Moderator"));
        create.EnsureSuccessStatusCode();
        var mod = await create.ReadJsonAsync<AdminUserResponse>();

        var transfer = await client.PostAsync($"/api/admin/users/{mod.Id}/transfer-admin", null);
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);
        var promoted = await transfer.ReadJsonAsync<AdminUserResponse>();
        promoted.IsAdmin.Should().BeTrue();
        promoted.IsModerator.Should().BeFalse();

        await client.LoginAsync(actorEmail);
        var users = await client.GetAsync("/api/admin/users");
        users.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Moderator_can_manage_plans_and_see_stats_but_not_users()
    {
        var client = CreateClient();
        var email = $"mod_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureModeratorRoleAsync(email);
        await client.LoginAsync(email);

        var plans = await client.GetAsync("/api/admin/subscription-plans");
        plans.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await client.GetAsync("/api/admin/stats");
        stats.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await client.GetAsync("/api/admin/users");
        users.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"u_{Guid.NewGuid():N}@example.com", "TestPass123!!", "New", "User", UserType.Individual, "User"));
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var userId = await _factory.GetUserIdAsync(email);
        var role = await client.PutAsJsonAsync($"/api/admin/users/{userId}/admin-role",
            new UpdateAdminRoleRequest(false));
        role.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Non_admin_gets_forbidden()
    {
        var client = CreateClient();
        var email = $"user_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);

        var response = await client.GetAsync("/api/admin/subscription-plans");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var usersResponse = await client.GetAsync("/api/admin/users");
        usersResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var statsResponse = await client.GetAsync("/api/admin/stats");
        statsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var createResponse = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"user_{Guid.NewGuid():N}@example.com", "TestPass123!!", "New", "User", UserType.Individual, "User"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_gets_unauthorized()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/admin/subscription-plans");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
