using Billing.API.DTOs;

namespace Billing.API.Services.Interfaces;

public interface IPlanService
{
    Task<IReadOnlyList<PlanDto>> GetActiveAsync(CancellationToken ct);
}
