using Billing.API.Data;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Repositories;

public class SubscriptionLifecycleRepository(BillingDbContext db) : ISubscriptionLifecycleRepository
{
    public async Task<IReadOnlyList<Subscription>> GetDueForExpiringReminderAsync(DateTime startUtc, DateTime endUtc,
        DateTime now, int take, CancellationToken ct) =>
        await db.Subscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate >= startUtc && s.EndDate < endUtc && s.EndDate > now &&
                !db.SubscriptionReminderLogs.Any(log => log.SubscriptionId == s.Id && log.Kind == ReminderKind.Expiring &&
                    log.EndDate == s.EndDate))
            .OrderBy(s => s.EndDate).Take(take).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetEndedActiveIdsAsync(DateTime now, int take, CancellationToken ct) =>
        await db.Subscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate <= now)
            .OrderBy(s => s.EndDate).Select(s => s.Id).Take(take).ToListAsync(ct);

    public Task<Subscription?> GetEndedActiveForUpdateAsync(Guid id, DateTime now, CancellationToken ct) =>
        db.Subscriptions.Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id && s.Status == SubscriptionStatus.Active && s.EndDate <= now, ct);

    public Task<bool> ReminderExistsAsync(Guid subscriptionId, string kind, DateTime endDate, CancellationToken ct) =>
        db.SubscriptionReminderLogs.AnyAsync(log => log.SubscriptionId == subscriptionId && log.Kind == kind &&
            log.EndDate == endDate, ct);

    public void Add(SubscriptionReminderLog log) => db.SubscriptionReminderLogs.Add(log);
}
