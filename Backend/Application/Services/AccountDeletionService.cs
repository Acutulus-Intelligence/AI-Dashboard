using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class AccountDeletionService : IAccountDeletionService
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _db;
    private readonly IPaymentService _paymentService;

    public AccountDeletionService(
        UserManager<User> userManager,
        IApplicationDbContext db,
        IPaymentService paymentService)
    {
        _userManager = userManager;
        _db = db;
        _paymentService = paymentService;
    }

    public async Task<int> PurgeExpiredAccountsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var userIds = await _db.Users
            .Where(u => u.DeletionScheduledAt != null && u.DeletionScheduledAt <= now)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            await PurgeAccountAsync(userId, ct);
        }

        return userIds.Count;
    }

    public async Task PurgeAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return;

        var dashboards = await _db.Dashboards
            .Include(d => d.Widgets)
            .Where(d => d.UserId == userId)
            .ToListAsync(ct);

        foreach (var dashboard in dashboards)
        {
            _db.DashboardWidgets.RemoveRange(dashboard.Widgets);
        }
        _db.Dashboards.RemoveRange(dashboards);

        var charts = await _db.SavedCharts
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);
        _db.SavedCharts.RemoveRange(charts);

        var connections = await _db.ExternalConnections
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);

        foreach (var connection in connections.Where(c => c.CompanyId is null).ToList())
        {
            _db.ExternalConnections.Remove(connection);
        }

        foreach (var group in connections
            .Where(c => c.CompanyId is not null)
            .GroupBy(c => c.CompanyId!.Value))
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == group.Key, ct);
            if (company is null)
            {
                _db.ExternalConnections.RemoveRange(group);
                continue;
            }

            foreach (var connection in group)
            {
                if (connection.Visibility == ConnectionVisibility.Private)
                    _db.ExternalConnections.Remove(connection);
                else
                    connection.UserId = company.OwnerId;
            }
        }

        var refreshTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);
        _db.RefreshTokens.RemoveRange(refreshTokens);

        var recoveryCodes = await _db.TwoFactorRecoveryCodes
            .Where(c => c.UserId == userId)
            .ToListAsync(ct);
        _db.TwoFactorRecoveryCodes.RemoveRange(recoveryCodes);

        var userSubscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (userSubscription is not null)
        {
            if (userSubscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionImmediatelyAsync(
                    userSubscription.StripeSubscriptionId, ct);
            _db.UserSubscriptions.Remove(userSubscription);
        }

        user.CompanyId = null;
        user.CompanyRoleId = null;

        // Invalidate all in-flight JWTs before removing the user.
        await _userManager.UpdateSecurityStampAsync(user);

        await _db.SaveChangesAsync(ct);

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to purge account {userId}.");
    }
}
