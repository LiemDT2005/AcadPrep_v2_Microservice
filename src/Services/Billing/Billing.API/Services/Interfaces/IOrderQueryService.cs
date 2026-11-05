using Billing.API.DTOs;

namespace Billing.API.Services.Interfaces;

public interface IOrderQueryService
{
    Task<PagedDto<OrderListItemDto>> GetMineAsync(Guid userId, MyOrdersQuery query, CancellationToken ct);
    Task<OrderDetailDto?> GetMineByCodeAsync(Guid userId, string orderCode, CancellationToken ct);
    Task<PagedDto<AdminOrderRowDto>> GetAdminPageAsync(AdminOrdersQuery query, CancellationToken ct);
    Task<BillingResult<AdminOrderCsvExport>> ExportMonthAsync(AdminOrdersQuery query, CancellationToken ct);
}
