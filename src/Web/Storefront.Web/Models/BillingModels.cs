using System.ComponentModel.DataAnnotations;
using Storefront.Web.Validation;

namespace Storefront.Web.Models;

public record BillingPlanLimit(string Feature, int? LimitValue, string Period);
public record BillingPlan(Guid Id, string Code, string Name, string? Description,
    decimal Price, int DurationDays, IReadOnlyList<BillingPlanLimit> Limits);
public record BillingCheckoutResult(string OrderCode, string PaymentUrl, DateTimeOffset ExpiresAt, bool Reused);

public class BillingCheckoutInput
{
    [Display(Name = "Gói"), Required(ErrorMessage = ValidPlanForCheckoutAttribute.Message), ValidPlanForCheckout]
    public Guid? PlanId { get; set; }

    public bool Renew { get; set; }

    // Paid plans on sale for this request (not bound from the form).
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public IReadOnlyCollection<Guid>? AllowedPlanIds { get; set; }
}

// Code = ProblemDetails title for business errors (e.g. PlanNotPurchasable); never shown raw to users.
public record BillingApiResult<T>(int Status, T? Data = default,
    IReadOnlyDictionary<string, string[]>? Errors = null, string? Code = null)
{
    public bool Success => Status is >= 200 and < 300;
}

public record BillingSubscription(string Status, string PlanCode, string PlanName, bool IsPro,
    DateTimeOffset? StartDate, DateTimeOffset? EndDate, int? DaysRemaining);
public record BillingFeatureUsage(string Feature, int? LimitValue, int? Used, int? Remaining, bool UsageAvailable,
    string Period, DateTimeOffset? ResetAt);
public record BillingAiUsage(bool Available, long? TokenLimit, long? TokensUsed, long? Remaining, DateTimeOffset? ResetAt);
public record BillingUsage(string PlanCode, IReadOnlyList<BillingFeatureUsage> Features, BillingAiUsage Ai);
public record BillingGrantResult(Guid SubscriptionId, DateTimeOffset StartDate, DateTimeOffset EndDate);

public record BillingPage<T>(IReadOnlyList<T> Items, int PageIndex, int PageSize, int TotalCount, int TotalPages);
public record BillingOrderItem(string OrderCode, string PlanName, decimal Amount, string Kind, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? PaidAt);
public record BillingOrderDetail(string OrderCode, string PlanName, decimal Amount, string Kind, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, DateTimeOffset ExpiresAt);
public record BillingAdminOrder(string OrderCode, Guid UserId, string? UserEmail, string PlanCode, decimal Amount,
    string Kind, string Status, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt);
public record BillingCsvFile(byte[] Content, string FileName);

public class BillingOrderFilter
{
    [Display(Name = "Trạng thái"), RegularExpression("^(Pending|Paid|Failed|Expired)$", ErrorMessage = "Trạng thái không hợp lệ.")]
    public string? Status { get; set; }

    [Display(Name = "ID người dùng"), BillingGuid]
    public string? UserId { get; set; }

    [Display(Name = "Gói"), RegularExpression("^(FREE|PRO_MONTH|PRO_QUARTER)$", ErrorMessage = "Gói không hợp lệ.")]
    public string? PlanCode { get; set; }

    [Display(Name = "Từ ngày"), DataType(DataType.Date)]
    public DateOnly? From { get; set; }

    [Display(Name = "Đến ngày"), DataType(DataType.Date), BillingDateRange(nameof(From))]
    public DateOnly? To { get; set; }

    [Display(Name = "Tháng xuất CSV"), RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Chọn tháng theo dạng yyyy-MM.")]
    public string? Month { get; set; }

    [RegularExpression("^(createdAt|amount)$")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("^(asc|desc)$")]
    public string SortDir { get; set; } = "desc";

    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    // Shared query string (Billing API parameter names); paging/month are added by the caller.
    public IEnumerable<KeyValuePair<string, string>> ApiParameters()
    {
        if (!string.IsNullOrEmpty(Status)) yield return new("status", Status);
        if (!string.IsNullOrWhiteSpace(UserId)) yield return new("userId", UserId.Trim());
        if (!string.IsNullOrEmpty(PlanCode)) yield return new("planCode", PlanCode);
    }
}

public record BillingRevenueTotals(decimal Revenue, int PaidCount, int FailedCount, int PendingCount, int ExpiredCount);
public record BillingRevenuePoint(string Period, decimal Revenue, int PaidCount);
public record BillingRevenue(DateOnly From, DateOnly To, string GroupBy, BillingRevenueTotals Totals,
    IReadOnlyList<BillingRevenuePoint> Points);

public class BillingRevenueFilter
{
    [Display(Name = "Từ ngày"), DataType(DataType.Date)]
    public DateOnly? From { get; set; }

    [Display(Name = "Đến ngày"), DataType(DataType.Date), BillingDateRange(nameof(From))]
    public DateOnly? To { get; set; }

    [Display(Name = "Gom theo"), RegularExpression("^(day|month)$", ErrorMessage = "Chọn gom theo ngày hoặc tháng.")]
    public string GroupBy { get; set; } = "day";

    public IEnumerable<KeyValuePair<string, string>> ApiParameters()
    {
        if (From is { } from) yield return new("from", from.ToString("yyyy-MM-dd"));
        if (To is { } to) yield return new("to", to.ToString("yyyy-MM-dd"));
        yield return new("groupBy", GroupBy);
    }
}

public class BillingGrantInput
{
    // Generated once per form; resubmitting the same form never grants twice.
    [Required]
    public Guid RequestId { get; set; } = Guid.NewGuid();

    [Display(Name = "Người nhận (Learner)"), Required(ErrorMessage = "Vui lòng chọn người nhận."), BillingGuid]
    public string? UserId { get; set; }

    [Display(Name = "Gói"), Required(ErrorMessage = "Vui lòng chọn gói.")]
    public Guid? PlanId { get; set; }

    [Display(Name = "Số ngày"), Range(1, 365, ErrorMessage = "Số ngày phải từ 1 đến 365.")]
    public int? DurationDays { get; set; }

    [Display(Name = "Ghi chú"), StringLength(500, ErrorMessage = "Ghi chú có tối đa 500 ký tự.")]
    public string? Note { get; set; }
}
