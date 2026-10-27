using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.HostedServices;

public class AccountPurgeBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountPurgeBackgroundService> _logger;

    public AccountPurgeBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountPurgeBackgroundService> logger)
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
            await PurgeAsync(stoppingToken);

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

    private async Task PurgeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var deletionService = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            var purged = await deletionService.PurgeExpiredAccountsAsync(ct);

            if (purged > 0)
                _logger.LogInformation(
                    "Purged {Count} deactivated account(s) past the grace period.", purged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account purge sweep failed.");
        }
    }
}
