using Billing.API.DTOs;

namespace Billing.API.Services.Interfaces;

public record EffectiveSubscription(SubscriptionDto View, Guid PlanId);

public interface ISubscriptionService
{
    Task<SubscriptionDto> GetMineAsync(Guid userId, CancellationToken ct);

    // The plan whose limits apply right now: the valid Active plan, otherwise FREE.
    Task<EffectiveSubscription> ResolveAsync(Guid userId, CancellationToken ct);
}
