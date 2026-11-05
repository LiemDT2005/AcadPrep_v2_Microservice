using Billing.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Billing.API.Messaging;

// Publishes outbox events every 5 seconds, like Exam's submission outbox worker.
public class OutboxPublisherWorker(IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> options,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IOutboxPublisher>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning("Outbox run failed; will retry. Reason={Reason}", exception.GetType().Name); }
        }
    }
}
