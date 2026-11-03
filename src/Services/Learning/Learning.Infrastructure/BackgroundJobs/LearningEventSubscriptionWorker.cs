using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Learning.Infrastructure.BackgroundJobs;

public class LearningEventSubscriptionWorker(IServiceProvider services, ILogger<LearningEventSubscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { services.SubscribeEvents(); }
            catch (Exception ex) { logger.LogWarning(ex, "Learning event subscriptions will reconnect."); }
        }
    }
}
