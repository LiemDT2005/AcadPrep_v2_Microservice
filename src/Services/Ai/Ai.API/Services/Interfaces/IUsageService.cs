using Ai.API.DTOs;
using Ai.API.Entities;

namespace Ai.API.Services.Interfaces;

public interface IUsageService
{
    DateTime Now { get; }
    Task<AiUsageWindow?> GetCurrentWindowAsync(Guid userId, DateTime now, bool track, CancellationToken ct);
    // Throws AiConfigurationException when the plan has no configured limit (never implicit unlimited access).
    Task<long> GetLimitAsync(string planCode, CancellationToken ct);
    // Read-only views for the caller; they never open a window.
    Task<UsageDto> GetMyUsageAsync(Guid userId, CancellationToken ct);
    Task<DailyUsageResponseDto> GetMyDailyUsageAsync(Guid userId, CancellationToken ct);
}
