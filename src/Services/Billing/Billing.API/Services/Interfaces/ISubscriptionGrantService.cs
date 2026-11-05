using Billing.API.DTOs;

namespace Billing.API.Services.Interfaces;

public interface ISubscriptionGrantService
{
    Task<BillingResult<SubscriptionGrantedDto>> GrantAsync(GrantSubscriptionRequest request, Guid adminId,
        CancellationToken ct);
}
