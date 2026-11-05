using System.Text.Json;
using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

// Publishes pending outbox events in creation order. A failure (broker down, no Notification queue bound yet)
// leaves the message pending with an attempt count; it is retried on the next run. Never loses an event.
public class OutboxPublisher(IOutboxRepository outbox, IUnitOfWork unitOfWork, IServiceProvider services, TimeProvider clock,
    ILogger<OutboxPublisher> logger) : IOutboxPublisher
{
    private const int BatchSize = 50;

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var pending = await outbox.GetPendingForUpdateAsync(BatchSize, ct);
        if (pending.Count == 0) return 0;

        // Resolved only when there is work, so an idle service never opens a broker connection.
        var bus = services.GetRequiredService<IEventBus>();
        var published = 0;
        foreach (var message in pending)
        {
            try
            {
                switch (message.Type)
                {
                    case nameof(SubscriptionExpiringEvent):
                        await bus.PublishAsync(JsonSerializer.Deserialize<SubscriptionExpiringEvent>(message.Payload)!, ct);
                        break;
                    case nameof(SubscriptionActivatedEvent):
                        await bus.PublishAsync(JsonSerializer.Deserialize<SubscriptionActivatedEvent>(message.Payload)!, ct);
                        break;
                    case nameof(SubscriptionExpiredEvent):
                        await bus.PublishAsync(JsonSerializer.Deserialize<SubscriptionExpiredEvent>(message.Payload)!, ct);
                        break;
                    default:
                        throw new NotSupportedException($"Unknown outbox type {message.Type}.");
                }
                message.PublishedAt = clock.GetUtcNow().UtcDateTime;
                message.LastError = null;
                published++;
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                message.Attempts++;
                message.LastError = Truncate($"{exception.GetType().Name}: {exception.Message}", 500);
                await unitOfWork.SaveChangesAsync(ct);
                logger.LogWarning("Outbox event {Id} ({Type}) not published yet; will retry. Attempts={Attempts}",
                    message.Id, message.Type, message.Attempts);
                // Unknown types can never succeed; anything else (broker/consumer down) stops the batch.
                if (exception is not NotSupportedException) break;
            }
        }
        return published;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
