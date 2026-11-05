using Billing.API.DTOs;
using Billing.API.Services;

namespace Billing.API.Services.Interfaces;

public interface ICheckoutService
{
    // kind = OrderKind.Purchase or OrderKind.Renewal; amount, plan and duration always come from Billing data.
    Task<BillingResult<CheckoutResultDto>> CheckoutAsync(Guid userId, string? email, Guid planId, string kind,
        string clientIp, CancellationToken ct);
}
