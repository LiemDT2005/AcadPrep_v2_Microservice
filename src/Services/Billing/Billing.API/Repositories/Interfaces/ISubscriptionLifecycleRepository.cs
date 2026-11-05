using Billing.API.Entities;

namespace Billing.API.Repositories.Interfaces;

// UC-23.3 reminder/expiry scans and the reminder log that makes them idempotent.
public interface ISubscriptionLifecycleRepository
{
    // Active subscriptions ending in [startUtc, endUtc) and after `now` without an Expiring reminder for that EndDate;
    // EndDate order, plan included, not tracked.
    Task<IReadOnlyList<Subscription>> GetDueForExpiringReminderAsync(DateTime startUtc, DateTime endUtc, DateTime now,
        int take, CancellationToken ct);
    // Ids of Active subscriptions whose EndDate has passed, EndDate order.
    Task<IReadOnlyList<Guid>> GetEndedActiveIdsAsync(DateTime now, int take, CancellationToken ct);
    // Re-read tracked (plan included); null when a concurrent grant already extended or closed it.
    Task<Subscription?> GetEndedActiveForUpdateAsync(Guid id, DateTime now, CancellationToken ct);
    Task<bool> ReminderExistsAsync(Guid subscriptionId, string kind, DateTime endDate, CancellationToken ct);
    void Add(SubscriptionReminderLog log);
}
