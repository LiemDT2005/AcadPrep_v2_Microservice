using Billing.API.Services;

namespace Billing.API.Services.Interfaces;

public interface ISubscriptionLifecycleService
{
    Task<LifecycleRunResult> RunOnceAsync(CancellationToken ct);
}
