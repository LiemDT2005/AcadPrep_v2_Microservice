using Billing.API.Data;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Repositories;

public class PlanRepository(BillingDbContext db) : IPlanRepository
{
    public async Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken ct) =>
        await db.Plans.AsNoTracking().Where(plan => plan.IsActive).Include(plan => plan.Limits)
            .OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Code).ToListAsync(ct);
}
