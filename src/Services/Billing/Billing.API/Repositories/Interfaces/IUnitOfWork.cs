namespace Billing.API.Repositories.Interfaces;

// Repositories share the request-scoped BillingDbContext (same as Exam); this saves their tracked changes together
// and wraps multi-table writes (order + payment log, subscription + outbox) in one transaction.
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct);
    // Discards every tracked change, e.g. after a lost race, before re-reading.
    void ResetTracking();
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
