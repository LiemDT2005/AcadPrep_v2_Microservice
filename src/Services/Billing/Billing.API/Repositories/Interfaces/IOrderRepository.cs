using Billing.API.DTOs;
using Billing.API.Entities;

namespace Billing.API.Repositories.Interfaces;

public record OrderFilter(Guid? UserId, string? Status, string? PlanCode, DateTime? CreatedFromUtc,
    DateTime? CreatedToExclusiveUtc);

public interface IOrderRepository
{
    // Checkout (UC-21.2): the unexpired Pending order of this user and plan that already has a payment URL.
    Task<Order?> GetLivePendingAsync(Guid userId, Guid planId, DateTime now, CancellationToken ct);
    // Pending orders of this user and plan past ExpiresAt (tracked, to be closed before a new checkout).
    Task<IReadOnlyList<Order>> GetExpiredPendingForUpdateAsync(Guid userId, Guid planId, DateTime now, CancellationToken ct);
    // VNPay callbacks: tracked, with the Version concurrency token.
    Task<Order?> GetByCodeForUpdateAsync(string orderCode, CancellationToken ct);
    // Spec 5.3 job: marks every Pending order past ExpiresAt as Expired in one statement.
    Task<int> ExpireAllPendingAsync(DateTime now, CancellationToken ct);
    void Add(Order order);
    void Add(PaymentLog log);

    Task<PagedDto<OrderListItemDto>> GetUserPageAsync(Guid userId, string? status, DateTime now, int pageIndex,
        int pageSize, CancellationToken ct);
    Task<OrderDetailDto?> GetUserOrderAsync(Guid userId, string orderCode, DateTime now, CancellationToken ct);
    Task<PagedDto<AdminOrderRowDto>> GetAdminPageAsync(OrderFilter filter, string sortBy, string sortDir,
        DateTime now, int pageIndex, int pageSize, CancellationToken ct);
    // Returns at most maxRows + 1 rows so the caller can detect an oversized export.
    Task<IReadOnlyList<AdminOrderRowDto>> GetAdminExportAsync(OrderFilter filter, DateTime now, int maxRows,
        CancellationToken ct);
}
