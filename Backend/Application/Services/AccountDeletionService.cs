using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AccountDeletionService : IAccountDeletionService
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        UserManager<User> userManager,
        IApplicationDbContext db,
        IPaymentService paymentService,
        ILogger<AccountDeletionService> logger)
    {
        _userManager = userManager;
        _db = db;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<int> PurgeExpiredAccountsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var userIds = await _db.Users
            .Where(u => u.DeletionScheduledAt != null && u.DeletionScheduledAt <= now)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (userIds.Count > 0)
            _logger.LogInformation("Account purge sweep found {Count} expired account(s).", userIds.Count);

        foreach (var userId in userIds)
        {
            try
            {
                await PurgeAccountAsync(userId, ct);
            }
            catch (Exception ex)
            {
                // One bad account must not block the rest of the sweep; it will
                // be retried on the next pass.
                _logger.LogError(ex, "Failed to purge account {UserId}.", userId);
            }
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

        // Widgets reference charts with a Restrict FK, so unfile any widget that
        // points at one of this user's charts (for example a widget on a
        // company/shared dashboard) before the charts are removed. Otherwise the
        // user delete is blocked.
        var chartIds = charts.Select(c => c.Id).ToList();
        if (chartIds.Count > 0)
        {
            var referencingWidgets = await _db.DashboardWidgets
                .Where(w => w.SavedChartId != null && chartIds.Contains(w.SavedChartId!.Value))
                .ToListAsync(ct);

            foreach (var widget in referencingWidgets)
                widget.SavedChartId = null;
        }

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

        // Data collections are a required (Restrict) reference to their creator,
        // so they must be removed or reassigned before the user can be deleted.
        var collections = await _db.DataCollections
            .Where(c => c.CreatedById == userId)
            .ToListAsync(ct);

        foreach (var group in collections
            .Where(c => c.CompanyId is not null)
            .GroupBy(c => c.CompanyId!.Value))
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.Id == group.Key, ct);
            if (company is null)
            {
                _db.DataCollections.RemoveRange(group);
                continue;
            }

            foreach (var collection in group)
                collection.CreatedById = company.OwnerId;
        }

        foreach (var collection in collections.Where(c => c.CompanyId is null).ToList())
        {
            _db.DataCollections.Remove(collection);
        }

        var userSubscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (userSubscription is not null)
        {
            if (userSubscription.StripeSubscriptionId is not null)
            {
                try
                {
                    await _paymentService.CancelSubscriptionImmediatelyAsync(
                        userSubscription.StripeSubscriptionId, ct);
                }
                catch (Exception ex)
                {
                    // Billing cleanup must never block the account deletion.
                    _logger.LogWarning(ex,
                        "Failed to cancel the Stripe subscription while purging account {UserId}; continuing.",
                        userId);
                }
            }

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
