using Billing.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Billing.API.Messaging;

// Marks Pending orders past ExpiresAt as Expired every 5 minutes (spec 5.3).
public class OrderExpiryWorker(IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> options,
    ILogger<OrderExpiryWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        await Task.Yield();
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IOrderExpiryService>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning("Order expiry run failed; will retry. Reason={Reason}", exception.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
