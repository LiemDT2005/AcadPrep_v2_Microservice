using Billing.API.DTOs;

namespace Billing.API.Services.Interfaces;

public interface IRevenueCache
{
    Task<RevenueDto?> GetAsync(string key, CancellationToken ct);
    Task SetAsync(string key, RevenueDto value, CancellationToken ct);
    // Called after every committed order change (new order, paid, failed, expired) so revenue and the per-status
    // counts are never served stale.
    Task InvalidateAsync(CancellationToken ct);
}
