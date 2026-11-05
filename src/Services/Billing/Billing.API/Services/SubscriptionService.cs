using Billing.API.DTOs;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

public class SubscriptionService(ISubscriptionRepository subscriptions, TimeProvider clock) : ISubscriptionService
{
    public const string FreePlanCode = "FREE";

    public async Task<SubscriptionDto> GetMineAsync(Guid userId, CancellationToken ct) =>
        (await ResolveAsync(userId, ct)).View;

    public async Task<EffectiveSubscription> ResolveAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var latest = await subscriptions.GetLatestAsync(userId, ct);

        // Expiry is evaluated on read: exactly at EndDate the subscription is no longer effective.
        if (latest is { Status: SubscriptionStatus.Active } && latest.EndDate > now)
            return new EffectiveSubscription(new SubscriptionDto(SubscriptionViewStatus.Active, latest.Plan.Code,
                latest.Plan.Name, latest.Plan.Code != FreePlanCode, latest.StartDate, latest.EndDate,
                (int)Math.Ceiling((latest.EndDate - now).TotalDays)), latest.PlanId);

        // The effective Free plan comes from Billing data; never hardcode a plan.
        var free = await subscriptions.GetPlanByCodeAsync(FreePlanCode, ct)
            ?? throw new PricingConfigurationException();
        var view = latest is null
            ? new SubscriptionDto(SubscriptionViewStatus.Free, free.Code, free.Name, false, null, null, null)
            : new SubscriptionDto(SubscriptionViewStatus.Expired, free.Code, free.Name, false, null, latest.EndDate, 0);
        return new EffectiveSubscription(view, free.Id);
    }
}
