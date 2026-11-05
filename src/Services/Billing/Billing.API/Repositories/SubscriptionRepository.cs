using Billing.API.Data;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Billing.API.Repositories;

public class SubscriptionRepository(BillingDbContext db) : ISubscriptionRepository
{
    public Task<Subscription?> GetLatestAsync(Guid userId, CancellationToken ct) =>
        db.Subscriptions.AsNoTracking().Include(subscription => subscription.Plan)
            .Where(subscription => subscription.UserId == userId)
            .OrderByDescending(subscription => subscription.EndDate).ThenByDescending(subscription => subscription.Id)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasAnyAsync(Guid userId, CancellationToken ct) =>
        db.Subscriptions.AnyAsync(subscription => subscription.UserId == userId, ct);

    public Task<Subscription?> GetActiveForUpdateAsync(Guid userId, CancellationToken ct) =>
        db.Subscriptions.FirstOrDefaultAsync(subscription =>
            subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active, ct);

    public Task<Plan?> GetPlanAsync(Guid planId, CancellationToken ct) =>
        db.Plans.AsNoTracking().FirstOrDefaultAsync(plan => plan.Id == planId, ct);

    public Task<Plan?> GetPlanByCodeAsync(string code, CancellationToken ct) =>
        db.Plans.AsNoTracking().FirstOrDefaultAsync(plan => plan.Code == code, ct);

    public Task<Plan?> GetPlanWithLimitsAsync(Guid planId, CancellationToken ct) =>
        db.Plans.AsNoTracking().Include(plan => plan.Limits).FirstOrDefaultAsync(plan => plan.Id == planId, ct);

    public Task<SubscriptionGrant?> GetGrantByRequestIdAsync(Guid requestId, CancellationToken ct) =>
        db.SubscriptionGrants.AsNoTracking().FirstOrDefaultAsync(grant => grant.RequestId == requestId, ct);

    public void Add(Subscription subscription) => db.Subscriptions.Add(subscription);
    public void Add(SubscriptionGrant grant) => db.SubscriptionGrants.Add(grant);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct) =>
        await db.Database.BeginTransactionAsync(ct);

    public Task CommitAsync(IAsyncDisposable transaction, CancellationToken ct) =>
        ((IDbContextTransaction)transaction).CommitAsync(ct);

    public void ResetTracking() => db.ChangeTracker.Clear();
}
