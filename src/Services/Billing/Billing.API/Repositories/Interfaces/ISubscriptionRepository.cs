using Billing.API.Entities;

namespace Billing.API.Repositories.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetLatestAsync(Guid userId, CancellationToken ct);
    // Renewal checkout requires a subscription history (any status).
    Task<bool> HasAnyAsync(Guid userId, CancellationToken ct);
    Task<Subscription?> GetActiveForUpdateAsync(Guid userId, CancellationToken ct);
    Task<Plan?> GetPlanAsync(Guid planId, CancellationToken ct);
    Task<Plan?> GetPlanByCodeAsync(string code, CancellationToken ct);
    Task<Plan?> GetPlanWithLimitsAsync(Guid planId, CancellationToken ct);
    Task<SubscriptionGrant?> GetGrantByRequestIdAsync(Guid requestId, CancellationToken ct);
    void Add(Subscription subscription);
    void Add(SubscriptionGrant grant);
    Task SaveChangesAsync(CancellationToken ct);
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct);
    Task CommitAsync(IAsyncDisposable transaction, CancellationToken ct);
    void ResetTracking();
}
