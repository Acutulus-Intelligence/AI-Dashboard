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
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null, 14));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();
        plan.StripeProductId.Should().NotBeNullOrEmpty();
        plan.StripeMonthlyPriceId.Should().NotBeNullOrEmpty();
        plan.StripeYearlyPriceId.Should().NotBeNullOrEmpty();
        plan.TrialDays.Should().Be(14);

        var byId = await client.GetAsync($"/api/admin/subscription-plans/{plan.Id}");
        byId.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{plan.Id}",
            new UpdateSubscriptionPlanRequest(
                plan.Name, "Updated", UserType.Individual, 19.99m, 199.99m, null, null, null, true, 0));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedPlan = await update.ReadJsonAsync<AdminSubscriptionPlanResponse>();
        updatedPlan.StripeProductId.Should().NotBeNullOrEmpty();
        updatedPlan.MonthlyPrice.Should().Be(19.99m);
        updatedPlan.TrialDays.Should().Be(0);

        var deactivate = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{plan.Id}",
            new UpdateSubscriptionPlanRequest(
                updatedPlan.Name, updatedPlan.Description, UserType.Individual, 19.99m, 199.99m, null, null, null, false, null));
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/admin/subscription-plans/{plan.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetAsync("/api/admin/subscription-plans");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.OK);
        var remaining = await afterDelete.ReadJsonAsync<List<AdminSubscriptionPlanResponse>>();
        remaining.Should().NotContain(p => p.Id == plan.Id);
    }

    [Fact]
    public async Task Admin_cannot_create_plan_with_invalid_trial_days()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureSoleAdminRoleAsync(email);
        await client.LoginAsync(email);

        var negative = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null, -1));
        negative.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var tooLong = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null, 366));
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Public_plans_expose_trial_days()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureSoleAdminRoleAsync(email);
        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null, 21));
        create.EnsureSuccessStatusCode();
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var publicPlans = await (await client.GetAsync("/api/subscriptions/plans?userType=0"))
            .ReadJsonAsync<List<SubscriptionPlanResponse>>();
        publicPlans.Should().Contain(p => p.Id == plan.Id && p.TrialDays == 21);
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
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null, null));
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
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null, null));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var userEmail = $"user_{Guid.NewGuid():N}@example.com";
        await client.RegisterAsync(userEmail);
        await client.LoginAsync(email);
        var userId = await _factory.GetUserIdAsync(userEmail);
        await _factory.SeedSubscriptionForPlanAsync(userId, plan.Id);

        var deactivate = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{plan.Id}",
            new UpdateSubscriptionPlanRequest(
                plan.Name, plan.Description, UserType.Individual, 9.99m, 99.99m, null, null, null, false, null));
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
                $"PlanA-{Guid.NewGuid():N}", "Test plan A", UserType.Individual, 9.99m, 99.99m, null, null, null, null));
        createA.StatusCode.Should().Be(HttpStatusCode.OK);
        var planA = await createA.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var createB = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"PlanB-{Guid.NewGuid():N}", "Test plan B", UserType.Individual, 19.99m, 199.99m, null, null, null, null));
        createB.StatusCode.Should().Be(HttpStatusCode.OK);
        var planB = await createB.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var userEmail = $"user_{Guid.NewGuid():N}@example.com";
        await client.RegisterAsync(userEmail);
        await client.LoginAsync(email);
        var userId = await _factory.GetUserIdAsync(userEmail);
        await _factory.SeedSubscriptionForPlanAsync(userId, planA.Id);

        var deactivateA = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{planA.Id}",
            new UpdateSubscriptionPlanRequest(
                planA.Name, planA.Description, UserType.Individual, 9.99m, 99.99m, null, null, null, false, null));
        deactivateA.StatusCode.Should().Be(HttpStatusCode.OK);

        var move = await client.PostAsJsonAsync($"/api/admin/subscription-plans/{planA.Id}/move",
            new MoveSubscriptionPlanRequest(planB.Id));
        move.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.VerifySubscriptionOnPlanAsync(userId, planB.Id, 19.99m);
    }

    [Fact]
    public async Task Move_plan_skips_subscriptions_scheduled_to_cancel()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureSoleAdminRoleAsync(email);
        await client.LoginAsync(email);

        var createA = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"PlanA-{Guid.NewGuid():N}", "Test plan A", UserType.Individual, 9.99m, 99.99m, null, null, null, null));
        createA.StatusCode.Should().Be(HttpStatusCode.OK);
        var planA = await createA.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var createB = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"PlanB-{Guid.NewGuid():N}", "Test plan B", UserType.Individual, 19.99m, 199.99m, null, null, null, null));
        createB.StatusCode.Should().Be(HttpStatusCode.OK);
        var planB = await createB.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var userEmail = $"user_{Guid.NewGuid():N}@example.com";
        await client.RegisterAsync(userEmail);
        await client.LoginAsync(email);
        var userId = await _factory.GetUserIdAsync(userEmail);
        await _factory.SeedSubscriptionForPlanAsync(userId, planA.Id);

        // The subscription is scheduled to cancel at period end — it must not be moved
        await _factory.SetUserSubscriptionActiveAsync(userId, cancelAtPeriodEnd: true);

        var deactivateA = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{planA.Id}",
            new UpdateSubscriptionPlanRequest(
                planA.Name, planA.Description, UserType.Individual, 9.99m, 99.99m, null, null, null, false, null));
        deactivateA.StatusCode.Should().Be(HttpStatusCode.OK);

        var move = await client.PostAsJsonAsync($"/api/admin/subscription-plans/{planA.Id}/move",
            new MoveSubscriptionPlanRequest(planB.Id));
        move.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Still on plan A, still scheduled to cancel
        var (status, cancelAtPeriodEnd) = await _factory.GetUserSubscriptionStateAsync(userId);
        status.Should().Be(Domain.Enums.SubscriptionStatus.Active);
        cancelAtPeriodEnd.Should().BeTrue();
        await _factory.VerifySubscriptionOnPlanAsync(userId, planA.Id, 9.99m);
    }

    [Fact]
    public async Task Admin_can_list_users_but_cannot_promote_admin_role()
    {
        var client = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var targetEmail = $"target_{Guid.NewGuid():N}@example.com";

        await client.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureSoleAdminRoleAsync(actorEmail);
        await client.LoginAsync(actorEmail);

        (await client.RegisterAsync(targetEmail)).EnsureSuccessStatusCode();
        await client.LoginAsync(actorEmail);
        var targetId = await _factory.GetUserIdAsync(targetEmail);

        var list = await client.GetAsync("/api/admin/users");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await list.ReadJsonAsync<List<AdminUserResponse>>();
        users.Should().Contain(u => u.Email == targetEmail);

        var promote = await client.PutAsJsonAsync($"/api/admin/users/{targetId}/admin-role",
            new { isAdmin = true });
        promote.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_cannot_revoke_another_admin()
    {
        var client = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var otherEmail = $"admin_{Guid.NewGuid():N}@example.com";

        await client.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureSoleAdminRoleAsync(actorEmail);
        await client.LoginAsync(actorEmail);

        (await client.RegisterAsync(otherEmail)).EnsureSuccessStatusCode();
        await _factory.EnsureAdminRoleAsync(otherEmail);
        await client.LoginAsync(actorEmail);
        var otherId = await _factory.GetUserIdAsync(otherEmail);

        var revoke = await client.PutAsJsonAsync($"/api/admin/users/{otherId}/admin-role",
            new { isAdmin = false });
        revoke.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_cannot_revoke_own_admin_role()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureSoleAdminRoleAsync(email);
        await client.LoginAsync(email);

        var actorId = await _factory.GetUserIdAsync(email);
        var response = await client.PutAsJsonAsync($"/api/admin/users/{actorId}/admin-role",
            new { isAdmin = false });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    public async Task Admin_cannot_create_user_with_admin_role()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureSoleAdminRoleAsync(email);
        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"newadmin_{Guid.NewGuid():N}@example.com", "TestPass123!!", "New", "Admin", UserType.Individual, "Admin"));
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        await _factory.EnsureSoleAdminRoleAsync(actorEmail);
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
    public async Task Admin_plan_price_change_updates_existing_subscription_at_renewal()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureSoleAdminRoleAsync(email);
        await client.LoginAsync(email);

        var create = await client.PostAsJsonAsync("/api/admin/subscription-plans",
            new CreateSubscriptionPlanRequest(
                $"Plan-{Guid.NewGuid():N}", "Test plan", UserType.Individual, 9.99m, 99.99m, null, null, null, null));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var plan = await create.ReadJsonAsync<AdminSubscriptionPlanResponse>();

        var userEmail = $"user_{Guid.NewGuid():N}@example.com";
        await client.RegisterAsync(userEmail);
        await client.LoginAsync(email);
        var userId = await _factory.GetUserIdAsync(userEmail);
        await _factory.SeedSubscriptionForPlanAsync(userId, plan.Id);

        var stripeSubId = await _factory.GetSubscriptionStripeIdAsync(userId);
        stripeSubId.Should().NotBeNullOrEmpty();

        var update = await client.PutAsJsonAsync($"/api/admin/subscription-plans/{plan.Id}",
            new UpdateSubscriptionPlanRequest(
                plan.Name, plan.Description, UserType.Individual, 29.99m, 199.99m, null, null, null, true, null));
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.VerifySubscriptionOnPlanAsync(userId, plan.Id, 9.99m);

        _factory.FakePayments.PriceSwitches.Should().Contain(
            s => s.StripeSubscriptionId == stripeSubId
                && s.ProrationBehavior == "none");

        await client.LoginAsync(userEmail);
        var current = await (await client.GetAsync("/api/subscriptions/current")).ReadJsonAsync<UserSubscriptionResponse>();
        current.Price.Should().Be(9.99m);
        current.NextPrice.Should().Be(29.99m);
        current.NextPriceEffectiveDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Transfer_admin_revokes_refresh_tokens_for_both_users()
    {
        var actorClient = CreateClient();
        var targetClient = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var targetEmail = $"target_{Guid.NewGuid():N}@example.com";

        await actorClient.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureSoleAdminRoleAsync(actorEmail);
        await actorClient.LoginAsync(actorEmail);

        await targetClient.RegisterAndLoginAsync(targetEmail);
        await _factory.EnsureModeratorRoleAsync(targetEmail);
        await targetClient.LoginAsync(targetEmail);

        var targetId = await _factory.GetUserIdAsync(targetEmail);

        var transfer = await actorClient.PostAsync($"/api/admin/users/{targetId}/transfer-admin", null);
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);

        var actorRefresh = await actorClient.PostAsync("/api/auth/refresh", null);
        actorRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var targetRefresh = await targetClient.PostAsync("/api/auth/refresh", null);
        targetRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Transfer_admin_invalidates_inflight_access_tokens_for_both_users()
    {
        var actorClient = CreateClient();
        var targetClient = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var targetEmail = $"target_{Guid.NewGuid():N}@example.com";

        await actorClient.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureSoleAdminRoleAsync(actorEmail);
        await actorClient.LoginAsync(actorEmail);

        await targetClient.RegisterAndLoginAsync(targetEmail);
        await _factory.EnsureModeratorRoleAsync(targetEmail);
        await targetClient.LoginAsync(targetEmail);

        var targetId = await _factory.GetUserIdAsync(targetEmail);

        // Baseline: admin can manage users, moderator cannot.
        (await actorClient.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await targetClient.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var transfer = await actorClient.PostAsync($"/api/admin/users/{targetId}/transfer-admin", null);
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);

        // Security stamps were rotated: both in-flight access tokens are dead, so the
        // demoted admin can no longer call Admin APIs and the new admin must sign in again.
        (await actorClient.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await targetClient.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Re-login: actor is now a moderator, target is now the admin.
        await actorClient.LoginAsync(actorEmail);
        (await actorClient.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await targetClient.LoginAsync(targetEmail);
        (await targetClient.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Moderator_role_change_invalidates_target_access_token()
    {
        var adminClient = CreateClient();
        var modClient = CreateClient();
        var adminEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var modEmail = $"mod_{Guid.NewGuid():N}@example.com";

        await adminClient.RegisterAndLoginAsync(adminEmail);
        await _factory.EnsureSoleAdminRoleAsync(adminEmail);
        await adminClient.LoginAsync(adminEmail);

        await modClient.RegisterAndLoginAsync(modEmail);
        await _factory.EnsureModeratorRoleAsync(modEmail);
        await modClient.LoginAsync(modEmail);

        var modId = await _factory.GetUserIdAsync(modEmail);

        // Baseline: moderator can hit Admin/Moderator routes.
        (await modClient.GetAsync("/api/admin/stats")).StatusCode.Should().Be(HttpStatusCode.OK);

        var demote = await adminClient.PutAsJsonAsync($"/api/admin/users/{modId}/moderator-role",
            new UpdateModeratorRoleRequest(false));
        demote.StatusCode.Should().Be(HttpStatusCode.OK);

        // The security stamp was rotated: the in-flight token is dead and the user must
        // sign in again before their new (reduced) role applies.
        (await modClient.GetAsync("/api/admin/stats")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Re-login: no longer a moderator, so Admin/Moderator routes are forbidden.
        await modClient.LoginAsync(modEmail);
        (await modClient.GetAsync("/api/admin/stats")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_cannot_transfer_admin_role_when_another_admin_exists()
    {
        var client = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        var otherEmail = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureSoleAdminRoleAsync(actorEmail);
        (await client.RegisterAsync(otherEmail)).EnsureSuccessStatusCode();
        await _factory.EnsureAdminRoleAsync(otherEmail);
        await client.LoginAsync(actorEmail);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"mod_{Guid.NewGuid():N}@example.com", "TestPass123!!", "Next", "Admin", UserType.Individual, "Moderator"));
        create.EnsureSuccessStatusCode();
        var mod = await create.ReadJsonAsync<AdminUserResponse>();

        var transfer = await client.PostAsync($"/api/admin/users/{mod.Id}/transfer-admin", null);
        transfer.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_cannot_delete_account_without_transferring_admin_role()
    {
        var client = CreateClient();
        var email = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(email);
        await _factory.EnsureSoleAdminRoleAsync(email);
        await client.LoginAsync(email);

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(ApiClientExtensions.DefaultPassword)),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Former_admin_can_delete_account_after_transferring()
    {
        var client = CreateClient();
        var actorEmail = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(actorEmail);
        await _factory.EnsureSoleAdminRoleAsync(actorEmail);
        await client.LoginAsync(actorEmail);

        var create = await client.PostAsJsonAsync("/api/admin/users",
            new CreateUserRequest($"mod_{Guid.NewGuid():N}@example.com", "TestPass123!!", "Next", "Admin", UserType.Individual, "Moderator"));
        create.EnsureSuccessStatusCode();
        var mod = await create.ReadJsonAsync<AdminUserResponse>();

        var transfer = await client.PostAsync($"/api/admin/users/{mod.Id}/transfer-admin", null);
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);

        await client.LoginAsync(actorEmail);

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(ApiClientExtensions.DefaultPassword)),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
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
            new { isAdmin = false });
        role.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Staff_only_listing_returns_only_admins_and_moderators()
    {
        var client = CreateClient();
        var adminEmail = $"admin_{Guid.NewGuid():N}@example.com";
        await client.RegisterAndLoginAsync(adminEmail);
        await _factory.EnsureSoleAdminRoleAsync(adminEmail);
        await client.LoginAsync(adminEmail);

        var userEmail = $"user_{Guid.NewGuid():N}@example.com";
        (await client.RegisterAsync(userEmail)).EnsureSuccessStatusCode();
        var modEmail = $"mod_{Guid.NewGuid():N}@example.com";
        await client.RegisterAsync(modEmail);
        await _factory.EnsureModeratorRoleAsync(modEmail);
        await client.LoginAsync(adminEmail);

        var staff = await client.GetAsync("/api/admin/users?staffOnly=true");
        staff.StatusCode.Should().Be(HttpStatusCode.OK);
        var staffUsers = await staff.ReadJsonAsync<List<AdminUserResponse>>();
        staffUsers.Should().Contain(u => u.Email == adminEmail);
        staffUsers.Should().Contain(u => u.Email == modEmail);
        staffUsers.Should().NotContain(u => u.Email == userEmail);

        var all = await client.GetAsync("/api/admin/users");
        var allUsers = await all.ReadJsonAsync<List<AdminUserResponse>>();
        allUsers.Count.Should().BeGreaterThanOrEqualTo(staffUsers.Count);
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
