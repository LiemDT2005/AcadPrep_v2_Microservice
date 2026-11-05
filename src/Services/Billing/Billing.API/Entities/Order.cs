namespace Billing.API.Entities;

public static class OrderStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Failed = "Failed";
    public const string Expired = "Expired";
    public static readonly IReadOnlyList<string> All = [Pending, Paid, Failed, Expired];
}

public static class OrderKind
{
    public const string Purchase = "Purchase";
    public const string Renewal = "Renewal";
}

// Created only by checkout/renewal (UC-21.2/23.3). The plan fields are an immutable purchase snapshot,
// so later price/plan changes never alter an existing order or its activation.
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string OrderCode { get; set; }
    public Guid UserId { get; set; }
    public string? UserEmail { get; set; }
    public Guid PlanId { get; set; }
    public required string PlanCode { get; set; }
    public required string PlanName { get; set; }
    public int DurationDays { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Kind { get; set; } = OrderKind.Purchase;
    public string Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    // The signed VNPay link generated at checkout; returned unchanged when the same pending order is reused.
    public string? PaymentUrl { get; set; }
    public string? VnpTransactionNo { get; set; }
    public string? VnpResponseCode { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
