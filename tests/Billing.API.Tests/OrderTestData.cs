using Billing.API.Entities;

namespace Billing.API.Tests;

// Test-only orders; production orders are created only by checkout (VNPay task).
internal static class OrderTestData
{
    public static Order Order(Guid userId, string code, DateTime createdAt, string status = OrderStatus.Paid,
        decimal amount = 11, string planCode = "PRO_MONTH", string? email = null, DateTime? expiresAt = null) => new()
    {
        OrderCode = code, UserId = userId, UserEmail = email, PlanId = planCode == "PRO_QUARTER" ? BillingTestData.QuarterId : BillingTestData.MonthId,
        PlanCode = planCode, PlanName = "Snapshot " + planCode, DurationDays = 30, Amount = amount, Status = status,
        CreatedAt = createdAt, ExpiresAt = expiresAt ?? createdAt.AddMinutes(15),
        PaidAt = status == OrderStatus.Paid ? createdAt.AddMinutes(1) : null
    };
}
