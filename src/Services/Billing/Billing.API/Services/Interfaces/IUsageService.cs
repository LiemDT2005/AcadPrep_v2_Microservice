using Billing.API.DTOs;

namespace Billing.API.Services.Interfaces;

public interface IUsageService
{
    Task<UsageDto> GetMineAsync(Guid userId, CancellationToken ct);
}
