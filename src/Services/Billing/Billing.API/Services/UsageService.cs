using Billing.API.DTOs;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

public class UsageService(ISubscriptionService subscriptions, ISubscriptionRepository repository,
    IAiUsageClient ai, TimeProvider clock) : IUsageService
{
    public async Task<UsageDto> GetMineAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var effective = await subscriptions.ResolveAsync(userId, ct);
        var plan = await repository.GetPlanWithLimitsAsync(effective.PlanId, ct)
            ?? throw new PricingConfigurationException();

        // A missing limit row is a configuration error, never implicit unlimited access.
        // Usage counts come from Exam/Learning integration (next version), so they are reported unavailable.
        var features = BillingFeatures.All.Select(feature =>
        {
            var limit = plan.Limits.SingleOrDefault(item => item.Feature == feature)
                ?? throw new PricingConfigurationException();
            return new FeatureUsageDto(feature, limit.LimitValue, Used: null, Remaining: null,
                UsageAvailable: false, limit.Period, UsagePeriods.ResetAt(limit.Period, now));
        }).ToArray();

        // AI failure never breaks the rest of the endpoint.
        var aiUsage = await ai.GetMyUsageAsync(ct);
        return new UsageDto(plan.Code, features, aiUsage);
    }
}
