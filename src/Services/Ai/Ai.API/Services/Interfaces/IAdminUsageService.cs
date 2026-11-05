using Ai.API.DTOs;

namespace Ai.API.Services.Interfaces;

public interface IAdminUsageService
{
    Task<PagedDto<AdminUsageRowDto>> ListAsync(AdminUsageQuery query, CancellationToken ct);
    Task<IReadOnlyList<TokenLimitDto>> LimitsAsync(CancellationToken ct);
    // Null when the plan has no limit row.
    Task<TokenLimitDto?> UpdateLimitAsync(Guid adminId, string planCode, long tokenLimit, CancellationToken ct);
    Task ResetAsync(Guid adminId, Guid userId, CancellationToken ct);
}
