using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Billing.API.Formatters;
using Billing.API.Validation;

namespace Billing.API.DTOs;

public record PagedDto<T>(IReadOnlyList<T> Items, int PageIndex, int PageSize, int TotalCount, int TotalPages);

public record OrderListItemDto(string OrderCode, string PlanName, decimal Amount, string Kind, string Status,
    DateTime CreatedAt, DateTime? PaidAt);

// No raw VNPay data is ever returned.
public record OrderDetailDto(string OrderCode, string PlanName, decimal Amount, string Kind, string Status,
    DateTime CreatedAt, DateTime? PaidAt, DateTime ExpiresAt);

public record AdminOrderRowDto(string OrderCode, Guid UserId, string? UserEmail, string PlanCode, decimal Amount,
    string Kind, string Status, DateTime CreatedAt, DateTime? PaidAt);

public class MyOrdersQuery
{
    [RegularExpression("^(Pending|Paid|Failed|Expired)$")]
    public string? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int PageIndex { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public class AdminOrdersQuery : IValidatableObject
{
    [RegularExpression("^(Pending|Paid|Failed|Expired)$")]
    public string? Status { get; init; }

    [NotEmptyGuid]
    public Guid? UserId { get; init; }

    [RegularExpression("^(FREE|PRO_MONTH|PRO_QUARTER)$")]
    public string? PlanCode { get; init; }

    // Vietnam calendar dates, inclusive on both ends, applied to CreatedAt.
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    // CSV export scope: one Vietnam calendar month (yyyy-MM), required when Accept is text/csv.
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$")]
    public string? Month { get; init; }

    [RegularExpression("^(createdAt|amount)$")]
    public string SortBy { get; init; } = "createdAt";

    [RegularExpression("^(asc|desc)$")]
    public string SortDir { get; init; } = "desc";

    [Range(1, int.MaxValue)]
    public int PageIndex { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public DateOnly? MonthStart => Month is not null &&
        DateOnly.TryParseExact(Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            ? start : null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is { } from && To is { } to && from > to)
            yield return new ValidationResult("From must not be after To.", [nameof(From), nameof(To)]);
        if (Month is not null && (From is not null || To is not null))
            yield return new ValidationResult("Use either Month or From/To, not both.", [nameof(Month)]);
    }
}

// CSV body for one month of orders; same columns as the JSON rows.
public sealed class AdminOrderCsvExport(IReadOnlyList<AdminOrderRowDto> rows, string month) : ICsvExportable
{
    public string FileName => $"orders-{month}.csv";

    public IReadOnlyList<string> Header { get; } =
        ["orderCode", "userId", "userEmail", "planCode", "amount", "kind", "status", "createdAt", "paidAt"];

    public IEnumerable<IReadOnlyList<string?>> Rows => rows.Select(row => (IReadOnlyList<string?>)
    [
        row.OrderCode, row.UserId.ToString("D"), row.UserEmail, row.PlanCode,
        row.Amount.ToString("0", CultureInfo.InvariantCulture), row.Kind, row.Status,
        row.CreatedAt.ToString("O", CultureInfo.InvariantCulture), row.PaidAt?.ToString("O", CultureInfo.InvariantCulture)
    ]);
}
