using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Notifications.API.Services;
using Notifications.API.Services.Interfaces;

namespace Notifications.API.Messaging;

// RabbitMQ consumers for Billing's subscription events (UC-21.2, UC-23.3).
public class SubscriptionExpiringHandler(ISubscriptionReminderService reminders) : IIntegrationEventHandler<SubscriptionExpiringEvent>
{
    public Task HandleAsync(SubscriptionExpiringEvent @event, CancellationToken cancellationToken = default) =>
        reminders.HandleAsync(SubscriptionReminderService.Expiring(@event), cancellationToken);
}

public class SubscriptionExpiredHandler(ISubscriptionReminderService reminders) : IIntegrationEventHandler<SubscriptionExpiredEvent>
{
    public Task HandleAsync(SubscriptionExpiredEvent @event, CancellationToken cancellationToken = default) =>
        reminders.HandleAsync(SubscriptionReminderService.Expired(@event), cancellationToken);
}

public class SubscriptionActivatedHandler(ISubscriptionReminderService reminders) : IIntegrationEventHandler<SubscriptionActivatedEvent>
{
    public Task HandleAsync(SubscriptionActivatedEvent @event, CancellationToken cancellationToken = default) =>
        reminders.HandleAsync(SubscriptionReminderService.Activated(@event), cancellationToken);
}
