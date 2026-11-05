using Notifications.API.Services;

namespace Notifications.API.Services.Interfaces;

public interface ISubscriptionReminderService
{
    // In-app notification + email for one Billing event; idempotent per event Id.
    Task HandleAsync(ReminderMessage message, CancellationToken ct);
}
