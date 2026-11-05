using Billing.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Billing.API.Messaging;

// Hourly reminder/expiry scan (first run at startup).
public class SubscriptionLifecycleWorker(IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> options,
    ILogger<SubscriptionLifecycleWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        await Task.Yield(); // never block host startup
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISubscriptionLifecycleService>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning("Subscription lifecycle run failed; will retry. Reason={Reason}", exception.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
