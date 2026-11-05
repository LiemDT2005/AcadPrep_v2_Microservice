using Billing.API.Data;
using Billing.API.DTOs;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Repositories;

// All filtering, effective-status evaluation, sorting and paging run in the database.
public class OrderRepository(BillingDbContext db) : IOrderRepository
{
    public Task<Order?> GetLivePendingAsync(Guid userId, Guid planId, DateTime now, CancellationToken ct) =>
        db.Orders.AsNoTracking().FirstOrDefaultAsync(order => order.UserId == userId && order.PlanId == planId &&
            order.Status == OrderStatus.Pending && order.ExpiresAt > now && order.PaymentUrl != null, ct);

    public async Task<IReadOnlyList<Order>> GetExpiredPendingForUpdateAsync(Guid userId, Guid planId, DateTime now,
        CancellationToken ct) =>
        await db.Orders.Where(order => order.UserId == userId && order.PlanId == planId &&
            order.Status == OrderStatus.Pending && order.ExpiresAt <= now).ToListAsync(ct);

    public Task<Order?> GetByCodeForUpdateAsync(string orderCode, CancellationToken ct) =>
        db.Orders.FirstOrDefaultAsync(item => item.OrderCode == orderCode, ct);

    public Task<int> ExpireAllPendingAsync(DateTime now, CancellationToken ct)
    {
        // Computed outside the expression so EF can translate the bulk update.
        var version = Guid.NewGuid();
        return db.Orders.Where(order => order.Status == OrderStatus.Pending && order.ExpiresAt <= now)
            .ExecuteUpdateAsync(set => set.SetProperty(order => order.Status, OrderStatus.Expired)
                .SetProperty(order => order.Version, version), ct);
    }

    public void Add(Order order) => db.Orders.Add(order);

    public void Add(PaymentLog log) => db.PaymentLogs.Add(log);

    public async Task<PagedDto<OrderListItemDto>> GetUserPageAsync(Guid userId, string? status, DateTime now,
        int pageIndex, int pageSize, CancellationToken ct)
    {
        var query = Filter(db.Orders.AsNoTracking().Where(order => order.UserId == userId), status, now);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(order => new OrderListItemDto(order.OrderCode, order.PlanName, order.Amount, order.Kind,
                order.Status == OrderStatus.Pending && order.ExpiresAt <= now ? OrderStatus.Expired : order.Status,
                order.CreatedAt, order.PaidAt))
            .ToListAsync(ct);
        return Page(items, pageIndex, pageSize, total);
    }

    public Task<OrderDetailDto?> GetUserOrderAsync(Guid userId, string orderCode, DateTime now, CancellationToken ct) =>
        db.Orders.AsNoTracking().Where(order => order.UserId == userId && order.OrderCode == orderCode)
            .Select(order => new OrderDetailDto(order.OrderCode, order.PlanName, order.Amount, order.Kind,
                order.Status == OrderStatus.Pending && order.ExpiresAt <= now ? OrderStatus.Expired : order.Status,
                order.CreatedAt, order.PaidAt, order.ExpiresAt))
            .FirstOrDefaultAsync(ct);

    public async Task<PagedDto<AdminOrderRowDto>> GetAdminPageAsync(OrderFilter filter, string sortBy, string sortDir,
        DateTime now, int pageIndex, int pageSize, CancellationToken ct)
    {
        var query = Admin(filter, now);
        var total = await query.CountAsync(ct);
        var desc = sortDir == "desc";
        var sorted = sortBy == "amount"
            ? desc ? query.OrderByDescending(order => order.Amount).ThenByDescending(order => order.Id)
                : query.OrderBy(order => order.Amount).ThenBy(order => order.Id)
            : desc ? query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
                : query.OrderBy(order => order.CreatedAt).ThenBy(order => order.Id);
        var items = await Rows(sorted.Skip((pageIndex - 1) * pageSize).Take(pageSize), now).ToListAsync(ct);
        return Page(items, pageIndex, pageSize, total);
    }

    public async Task<IReadOnlyList<AdminOrderRowDto>> GetAdminExportAsync(OrderFilter filter, DateTime now,
        int maxRows, CancellationToken ct) =>
        await Rows(Admin(filter, now).OrderBy(order => order.CreatedAt).ThenBy(order => order.Id).Take(maxRows + 1), now)
            .ToListAsync(ct);

    private IQueryable<Order> Admin(OrderFilter filter, DateTime now)
    {
        var query = Filter(db.Orders.AsNoTracking(), filter.Status, now);
        if (filter.UserId is { } userId) query = query.Where(order => order.UserId == userId);
        if (filter.PlanCode is { } planCode) query = query.Where(order => order.PlanCode == planCode);
        if (filter.CreatedFromUtc is { } from) query = query.Where(order => order.CreatedAt >= from);
        if (filter.CreatedToExclusiveUtc is { } to) query = query.Where(order => order.CreatedAt < to);
        return query;
    }

    // A Pending order past ExpiresAt is Expired on read, even before the expiry job runs.
    private static IQueryable<Order> Filter(IQueryable<Order> query, string? status, DateTime now) => status switch
    {
        null => query,
        OrderStatus.Pending => query.Where(order => order.Status == OrderStatus.Pending && order.ExpiresAt > now),
        OrderStatus.Expired => query.Where(order => order.Status == OrderStatus.Expired ||
            order.Status == OrderStatus.Pending && order.ExpiresAt <= now),
        _ => query.Where(order => order.Status == status)
    };

    private static IQueryable<AdminOrderRowDto> Rows(IQueryable<Order> query, DateTime now) =>
        query.Select(order => new AdminOrderRowDto(order.OrderCode, order.UserId, order.UserEmail, order.PlanCode,
            order.Amount, order.Kind,
            order.Status == OrderStatus.Pending && order.ExpiresAt <= now ? OrderStatus.Expired : order.Status,
            order.CreatedAt, order.PaidAt));

    private static PagedDto<T> Page<T>(IReadOnlyList<T> items, int pageIndex, int pageSize, int total) =>
        new(items, pageIndex, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
}
