using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.HostedServices;

/// <summary>
/// Periodically ends subscriptions whose paid period has passed, using Stripe as the source of
/// truth so a missed webhook can never leave an Active row hanging forever. Also repairs rows
/// whose local EndDate drifted because a renewal event was lost.
/// </summary>
public class SubscriptionExpiryBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionExpiryBackgroundService> _logger;

    public SubscriptionExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SubscriptionExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            await ReconcileAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
            var changed = await subscriptionService.ReconcileSubscriptionsAsync(ct);

            if (changed > 0)
                _logger.LogInformation("Reconciled {Count} ended subscription(s).", changed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscription reconciliation sweep failed.");
        }
    }
}
