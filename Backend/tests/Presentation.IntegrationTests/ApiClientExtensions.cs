using System.Net.Http.Json;
using System.Text.Json;
using Application.DTos.Request;
using Domain.Enums;
using Domain.Models;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.IntegrationTests;

public static class ApiClientExtensions
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public const string DefaultPassword = "TestPass123!!";

    public static async Task<HttpResponseMessage> RegisterAsync(
        this HttpClient client,
        string email,
        string password = DefaultPassword,
        string firstName = "Test",
        string lastName = "User",
        UserType userType = UserType.Individual,
        string? inviteToken = null)
    {
        return await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, password, firstName, lastName, userType, inviteToken));
    }

    public static async Task<HttpResponseMessage> LoginAsync(
        this HttpClient client,
        string email,
        string password = DefaultPassword)
    {
        return await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
    }

    public static async Task RegisterAndLoginAsync(
        this HttpClient client,
        string email,
        string password = DefaultPassword)
    {
        var register = await client.RegisterAsync(email, password);
        register.EnsureSuccessStatusCode();
    }

    public static async Task SeedActiveSubscriptionAsync(this ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} not found for subscription seed.");

        var plan = db.SubscriptionPlans.First(p => p.UserType == UserType.Individual && p.IsActive);

        await SeedSubscriptionForPlanAsync(factory, user.Id, plan.Id, "sub_seed");
    }

    public static async Task SeedSubscriptionForPlanAsync(
        this ApiFactory factory, Guid userId, Guid planId, string subPrefix = "sub_seed")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plan = db.SubscriptionPlans.First(p => p.Id == planId);

        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = planId,
            Price = plan.MonthlyPrice,
            BillingPeriod = BillingPeriod.Monthly,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = SubscriptionStatus.Trial,
            StripeSubscriptionId = $"{subPrefix}_{Guid.NewGuid():N}",
            TrialEndDate = DateTime.UtcNow.AddDays(7),
        });

        await db.SaveChangesAsync();
    }

    public static async Task SeedCompanySubscriptionAsync(this ApiFactory factory, Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plan = db.SubscriptionPlans.First(p => p.UserType == UserType.Company && p.IsActive);

        db.CompanySubscriptions.Add(new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanId = plan.Id,
            Price = plan.MonthlyPrice,
            BillingPeriod = BillingPeriod.Monthly,
            MaxUsers = plan.MaxUsers,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = SubscriptionStatus.Trial,
            StripeSubscriptionId = $"sub_seed_co_{Guid.NewGuid():N}",
            TrialEndDate = DateTime.UtcNow.AddDays(7),
        });

        await db.SaveChangesAsync();
    }

    public static async Task<Guid> GetUserIdAsync(this ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} not found.");
        return user.Id;
    }

    public static async Task VerifySubscriptionStillActiveAsync(this ApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        subscription.Should().NotBeNull();
        subscription!.Status.Should().Be(SubscriptionStatus.Trial);
    }

    public static async Task VerifySubscriptionOnPlanAsync(this ApiFactory factory, Guid userId, Guid planId, decimal price)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);
        subscription.Should().NotBeNull();
        subscription!.PlanId.Should().Be(planId);
        subscription.Price.Should().Be(price);
    }

    public static async Task<string?> GetSubscriptionStripeIdAsync(this ApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.UserSubscriptions
            .Where(s => s.UserId == userId)
            .Select(s => s.StripeSubscriptionId)
            .FirstOrDefaultAsync();
    }

    public static async Task SetNextPriceAsync(this ApiFactory factory, Guid userId, decimal nextPrice)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.UserSubscriptions.FirstAsync(s => s.UserId == userId);
        subscription.NextPrice = nextPrice;
        subscription.NextPriceEffectiveDate = DateTime.UtcNow.AddDays(10);
        await db.SaveChangesAsync();
    }

    public static async Task<int> CountUserSubscriptionsAsync(this ApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.UserSubscriptions.CountAsync(s => s.UserId == userId);
    }

    public static async Task<(Domain.Enums.SubscriptionStatus Status, bool CancelAtPeriodEnd)> GetUserSubscriptionStateAsync(this ApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.UserSubscriptions.FirstAsync(s => s.UserId == userId);
        return (subscription.Status, subscription.CancelAtPeriodEnd);
    }

    public static async Task SetUserSubscriptionEndDateAsync(this ApiFactory factory, Guid userId, DateTime endDate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.UserSubscriptions.FirstAsync(s => s.UserId == userId);
        subscription.EndDate = endDate;
        await db.SaveChangesAsync();
    }

    public static async Task SetUserSubscriptionActiveAsync(this ApiFactory factory, Guid userId, bool cancelAtPeriodEnd)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await db.UserSubscriptions.FirstAsync(s => s.UserId == userId);
        subscription.Status = Domain.Enums.SubscriptionStatus.Active;
        subscription.CancelAtPeriodEnd = cancelAtPeriodEnd;
        subscription.TrialEndDate = null;
        await db.SaveChangesAsync();
    }

    public static async Task EnsureAdminRoleAsync(this ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} not found.");
        if (!await users.IsInRoleAsync(user, "Admin"))
            await users.AddToRoleAsync(user, "Admin");
    }

    public static async Task EnsureSoleAdminRoleAsync(this ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} not found.");

        var admins = await users.GetUsersInRoleAsync("Admin");
        foreach (var admin in admins.Where(a => a.Id != user.Id))
            await users.RemoveFromRoleAsync(admin, "Admin");

        if (!await users.IsInRoleAsync(user, "Admin"))
            await users.AddToRoleAsync(user, "Admin");
    }

    public static async Task EnsureModeratorRoleAsync(this ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} not found.");
        if (!await users.IsInRoleAsync(user, "Moderator"))
            await users.AddToRoleAsync(user, "Moderator");
    }

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return payload ?? throw new InvalidOperationException("Response body was empty.");
    }
}
