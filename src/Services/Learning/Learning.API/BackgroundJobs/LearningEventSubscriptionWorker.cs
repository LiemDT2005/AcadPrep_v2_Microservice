using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Learning.API.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Learning.API.BackgroundJobs;

public class LearningEventSubscriptionWorker(IServiceProvider services, ILogger<LearningEventSubscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var bus = services.GetRequiredService<IEventBus>();
                bus.Subscribe<ExamCompletedEvent, TestSubmittedHandler>();
                bus.Subscribe<TestSubmittedEvent, TestSubmittedHandler>();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Learning event subscriptions will reconnect.");
            }
        }
    }
}
