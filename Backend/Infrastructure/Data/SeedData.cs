using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var db = serviceProvider.GetRequiredService<AppDbContext>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var roles = new[] { "Admin", "User" };

        foreach (var role in roles)
        {
            var roleExists = await roleManager.RoleExistsAsync(role);
            if (!roleExists)
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        if (!db.SubscriptionPlans.Any())
        {
            var plans = new[]
            {      
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(),
                    Name = "Individual",
                    Description = "Plan for individual users",
                    UserType = UserType.Individual,
                    MonthlyPrice = 19.99m,
                    YearlyPrice = 199.99m,
                    MaxUsers = null,
                    MaxDashboards = null,
                    MaxAiQueriesPerMonth = null,
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(),
                    Name = "Company Starter",
                    Description = "Starter plan for small companies",
                    UserType = UserType.Company,
                    MonthlyPrice = 49.99m,
                    YearlyPrice = 499.99m,
                    MaxUsers = 5,
                    MaxDashboards = null,
                    MaxAiQueriesPerMonth = null,
                    IsActive = true
                },
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(),
                    Name = "Company Enterprise",
                    Description = "Enterprise plan for large companies",
                    UserType = UserType.Company,
                    MonthlyPrice = 149.99m,
                    YearlyPrice = 1499.99m,
                    MaxUsers = null,
                    MaxDashboards = null,
                    MaxAiQueriesPerMonth = null,
                    IsActive = true
                }
            };

            db.SubscriptionPlans.AddRange(plans);
            await db.SaveChangesAsync();
        }

        var adminEmail = configuration["Seed__AdminEmail"];
        var adminPassword = configuration["Seed__AdminPassword"];
        var adminUserName = configuration["Seed__AdminUserName"];

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
            return;

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new User
            {
                UserName = adminUserName ?? adminEmail,
                Email = adminEmail,
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true,
                UserType = UserType.Individual
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                await userManager.AddToRoleAsync(adminUser, "User");
            }
        }
    }
}
