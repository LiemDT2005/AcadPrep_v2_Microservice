using System.Text.RegularExpressions;
using Billing.API.DTOs;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

public partial class OrderQueryService(IOrderRepository orders, TimeProvider clock) : IOrderQueryService
{
    public const int MaxExportRows = 10_000;

    public Task<PagedDto<OrderListItemDto>> GetMineAsync(Guid userId, MyOrdersQuery query, CancellationToken ct) =>
        orders.GetUserPageAsync(userId, query.Status, Now, query.PageIndex, query.PageSize, ct);

    public Task<OrderDetailDto?> GetMineByCodeAsync(Guid userId, string orderCode, CancellationToken ct) =>
        // A malformed code is indistinguishable from someone else's order: 404, no lookup.
        OrderCodePattern().IsMatch(orderCode)
            ? orders.GetUserOrderAsync(userId, orderCode, Now, ct)
            : Task.FromResult<OrderDetailDto?>(null);

    public Task<PagedDto<AdminOrderRowDto>> GetAdminPageAsync(AdminOrdersQuery query, CancellationToken ct) =>
        orders.GetAdminPageAsync(Filter(query, StartUtc(query.From), query.To is { } to ? StartUtc(to.AddDays(1)) : null),
            query.SortBy, query.SortDir, Now, query.PageIndex, query.PageSize, ct);

    // CSV scope is one Vietnam calendar month of CreatedAt, with the other filters applied (Huy, 2026-11-05).
    public async Task<BillingResult<AdminOrderCsvExport>> ExportMonthAsync(AdminOrdersQuery query, CancellationToken ct)
    {
        if (query.MonthStart is not { } month)
            return BillingResult<AdminOrderCsvExport>.Failure(BillingError.Unprocessable, "Choose a month to export.");
        var rows = await orders.GetAdminExportAsync(
            Filter(query, StartUtc(month), StartUtc(month.AddMonths(1))), Now, MaxExportRows, ct);
        return rows.Count > MaxExportRows
            ? BillingResult<AdminOrderCsvExport>.Failure(BillingError.Unprocessable,
                $"The export exceeds {MaxExportRows} rows. Narrow the filters.")
            : BillingResult<AdminOrderCsvExport>.Success(new AdminOrderCsvExport(rows, query.Month!));
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static OrderFilter Filter(AdminOrdersQuery query, DateTime? fromUtc, DateTime? toExclusiveUtc) =>
        new(query.UserId, query.Status, query.PlanCode, fromUtc, toExclusiveUtc);

    // 00:00 Vietnam time (UTC+7) of the given date, as UTC.
    private static DateTime? StartUtc(DateOnly? date) => date is { } value
        ? DateTime.SpecifyKind(value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) - UsagePeriods.VietnamOffset
        : null;

    [GeneratedRegex("^[A-Za-z0-9]{1,20}$")]
    private static partial Regex OrderCodePattern();
}
