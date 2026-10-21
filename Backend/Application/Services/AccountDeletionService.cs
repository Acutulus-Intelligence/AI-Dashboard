using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AccountDeletionService : IAccountDeletionService
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        UserManager<User> userManager,
        IApplicationDbContext db,
        IPaymentService paymentService,
        IServiceScopeFactory scopeFactory,
        ILogger<AccountDeletionService> logger)
    {
        _userManager = userManager;
        _db = db;
        _paymentService = paymentService;
        _scopeFactory = scopeFactory;
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

        var purged = 0;

        foreach (var userId in userIds)
        {
            // Each account is purged in its own DI scope, so a failure (e.g. a
            // Stripe outage) can only roll back that account and never poisons
            // the shared ChangeTracker for the accounts that follow.
            using var scope = _scopeFactory.CreateScope();
            var deletionService = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();

            try
            {
                await deletionService.PurgeAccountAsync(userId, ct);
                purged++;
            }
            catch (Exception ex)
            {
                // One bad account must not block the rest of the sweep; it will
                // be retried on the next pass.
                _logger.LogError(ex, "Failed to purge account {UserId}; it will be retried on the next sweep.", userId);
            }
        }

        return purged;
    }

    public async Task PurgeAccountAsync(Guid userId, CancellationToken ct = default)
    {
        // Everything for a single account commits atomically, so a failure can
        // never leave the account half-deleted.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            await transaction.CommitAsync(ct);
            return;
        }

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

        // Widgets reference charts with a Restrict FK, so remove any widget that
        // points at one of this user's charts (for example a widget on a
        // company/shared dashboard) before the charts are removed. The chart is
        // gone, so leaving the widget behind would only render as "Unknown".
        var chartIds = charts.Select(c => c.Id).ToList();
        if (chartIds.Count > 0)
        {
            var referencingWidgets = await _db.DashboardWidgets
                .Where(w => w.SavedChartId != null && chartIds.Contains(w.SavedChartId!.Value))
                .ToListAsync(ct);

            _db.DashboardWidgets.RemoveRange(referencingWidgets);
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
                // If Stripe cannot cancel the subscription we must not delete the
                // account: dropping UserSubscription would lose the only reference
                // to a subscription the customer keeps being billed for. Letting
                // the failure bubble skips this account so the next sweep retries.
                await _paymentService.CancelSubscriptionImmediatelyAsync(
                    userSubscription.StripeSubscriptionId, ct);
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

        await transaction.CommitAsync(ct);
    }
}
