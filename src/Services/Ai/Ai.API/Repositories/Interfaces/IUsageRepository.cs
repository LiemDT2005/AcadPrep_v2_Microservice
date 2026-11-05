using Ai.API.Entities;

namespace Ai.API.Repositories.Interfaces;

// Usage windows, token limits and the Admin audit trail.
public interface IUsageRepository
{
    // The window that is still open at `now` (latest start); tracked only when the caller will change it.
    Task<AiUsageWindow?> GetCurrentWindowAsync(Guid userId, DateTime now, bool track, CancellationToken ct);
    // Active windows (WindowEnd > now), TokensUsed desc, then UserId.
    Task<(IReadOnlyList<AiUsageWindow> Items, int Total)> ListActiveWindowsAsync(DateTime now, Guid? userId, int pageIndex,
        int pageSize, CancellationToken ct);

    Task<AiTokenLimit?> GetLimitAsync(string planCode, CancellationToken ct);
    Task<AiTokenLimit?> GetLimitForUpdateAsync(string planCode, CancellationToken ct);
    // TokenLimit asc, then PlanCode.
    Task<IReadOnlyList<AiTokenLimit>> ListLimitsAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<string, long>> GetLimitMapAsync(CancellationToken ct);

    // Adds the audit row and saves it together with the tracked limit/window change it describes.
    Task SaveWithAuditAsync(AiUsageAudit audit, CancellationToken ct);
}
