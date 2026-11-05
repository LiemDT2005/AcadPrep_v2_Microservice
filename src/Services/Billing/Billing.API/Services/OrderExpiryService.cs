using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

// Spec 5.3: Pending orders past ExpiresAt are marked Expired every 5 minutes (reads already treat them so).
public class OrderExpiryService(IOrderRepository orders, IRevenueCache revenueCache, TimeProvider clock) : IOrderExpiryService
{
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var expired = await orders.ExpireAllPendingAsync(clock.GetUtcNow().UtcDateTime, ct);
        if (expired > 0) await revenueCache.InvalidateAsync(ct); // stored status counts changed
        return expired;
    }
}
