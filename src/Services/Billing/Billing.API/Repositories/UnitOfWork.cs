using Billing.API.Data;
using Billing.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Billing.API.Repositories;

public class UnitOfWork(BillingDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) =>
        new Transaction(await db.Database.BeginTransactionAsync(ct));

    public void ResetTracking() => db.ChangeTracker.Clear();

    private sealed class Transaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
