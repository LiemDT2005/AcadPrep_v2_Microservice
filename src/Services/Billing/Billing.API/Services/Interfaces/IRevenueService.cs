using Billing.API.DTOs;

namespace Billing.API.Services.Interfaces;

public interface IRevenueService
{
    // Returns a field-keyed error message (400) or the statistics.
    Task<(RevenueDto? Value, string? Field, string? Error)> GetAsync(RevenueQuery query, CancellationToken ct);
}
