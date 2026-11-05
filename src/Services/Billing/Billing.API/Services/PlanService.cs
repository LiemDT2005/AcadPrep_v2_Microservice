using Billing.API.DTOs;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

public class PlanService(IPlanRepository plans) : IPlanService
{
    public async Task<IReadOnlyList<PlanDto>> GetActiveAsync(CancellationToken ct)
    {
        var items = await plans.GetActiveAsync(ct);
        // Missing quota configuration must never silently become unlimited access.
        if (items.Any(plan => !new[] { "FullTest", "Practice", "Vocab" }
                .All(feature => plan.Limits.Any(limit => limit.Feature == feature))))
            throw new PricingConfigurationException();

        return items.Select(plan => new PlanDto(plan.Id, plan.Code, plan.Name, plan.Description,
            plan.Price, plan.DurationDays, plan.Limits.OrderBy(limit => limit.Feature)
                .Select(limit => new PlanLimitDto(limit.Feature, limit.LimitValue, limit.Period)).ToArray())).ToArray();
    }
}
