using System.Net.Http.Json;
using System.Text.Json;
using Application.Dtos.Request;
using Domain.Enums;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
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

        db.UserSubscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PlanId = plan.Id,
            Price = plan.MonthlyPrice,
            BillingPeriod = BillingPeriod.Monthly,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7),
            Status = SubscriptionStatus.Trial,
            StripeSubscriptionId = $"sub_seed_{Guid.NewGuid():N}",
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

    public static async Task EnsureAdminRoleAsync(this ApiFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} not found.");
        if (!await users.IsInRoleAsync(user, "Admin"))
            await users.AddToRoleAsync(user, "Admin");
    }

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return payload ?? throw new InvalidOperationException("Response body was empty.");
    }
}
