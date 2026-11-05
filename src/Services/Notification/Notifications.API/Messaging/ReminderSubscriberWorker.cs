using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Microsoft.Extensions.Options;

namespace Notifications.API.Messaging;

public class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";
    public bool Enabled { get; set; } = true;
}

// Keeps the Billing reminder consumers subscribed. Subscribe is idempotent per queue and reconnects when the
// broker was unavailable at startup or the connection dropped, so events queued in Billing's outbox are
// delivered once RabbitMQ and Notification are both up.
public class ReminderSubscriberWorker(IServiceProvider services, IOptions<BackgroundJobOptions> options,
    ILogger<ReminderSubscriberWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        // Never block host startup while the broker is being contacted.
        await Task.Yield();
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var bus = services.GetRequiredService<IEventBus>();
                bus.Subscribe<SubscriptionExpiringEvent, SubscriptionExpiringHandler>();
                bus.Subscribe<SubscriptionExpiredEvent, SubscriptionExpiredHandler>();
                bus.Subscribe<SubscriptionActivatedEvent, SubscriptionActivatedHandler>();
            }
            catch (Exception exception)
            {
                logger.LogWarning("Reminder subscription not ready; will retry. Reason={Reason}", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
