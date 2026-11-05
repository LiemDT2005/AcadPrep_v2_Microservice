using Billing.API.Data;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Repositories;

public class RevenueRepository(BillingDbContext db) : IRevenueRepository
{
    // Only the two needed columns of Paid orders in range are read (indexed by PaidAt); at most one
    // row per paid order, bucketed in memory by Vietnam day/month to stay provider-neutral.
    public async Task<IReadOnlyList<PaidAmount>> GetPaidAsync(DateTime fromUtc, DateTime toExclusiveUtc, CancellationToken ct) =>
        await db.Orders.AsNoTracking()
            .Where(order => order.Status == OrderStatus.Paid && order.PaidAt >= fromUtc && order.PaidAt < toExclusiveUtc)
            .Select(order => new PaidAmount(order.PaidAt!.Value, order.Amount))
            .ToListAsync(ct);

    public async Task<OrderStatusCounts> CountCreatedAsync(DateTime fromUtc, DateTime toExclusiveUtc, DateTime now,
        CancellationToken ct)
    {
        var created = db.Orders.AsNoTracking().Where(order => order.CreatedAt >= fromUtc && order.CreatedAt < toExclusiveUtc);
        // A Pending order past ExpiresAt counts as Expired (read-time status).
        return new OrderStatusCounts(
            await created.CountAsync(order => order.Status == OrderStatus.Paid, ct),
            await created.CountAsync(order => order.Status == OrderStatus.Failed, ct),
            await created.CountAsync(order => order.Status == OrderStatus.Pending && order.ExpiresAt > now, ct),
            await created.CountAsync(order => order.Status == OrderStatus.Expired ||
                order.Status == OrderStatus.Pending && order.ExpiresAt <= now, ct));
    }
}
