using Billing.API.Entities;

namespace Billing.API.Repositories.Interfaces;

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken ct);
}
