using Ai.API.DTOs;
using Ai.API.Entities;
using Ai.API.Entities.Enums;
using Ai.API.Repositories.Interfaces;
using Ai.API.Services.Interfaces;

namespace Ai.API.Services;

// UC-22.2 administration.
public class AdminUsageService(IUsageRepository repository, IUsageService usage) : IAdminUsageService
{
    public static readonly string[] Plans = ["FREE", "PRO_MONTH", "PRO_QUARTER"];

    public async Task<PagedDto<AdminUsageRowDto>> ListAsync(AdminUsageQuery query, CancellationToken ct)
    {
        var (rows, total) = await repository.ListActiveWindowsAsync(usage.Now, query.UserId, query.PageIndex, query.PageSize, ct);
        var limits = await repository.GetLimitMapAsync(ct);
        var items = rows.Select(window =>
        {
            var limit = limits.GetValueOrDefault(window.PlanCode);
            return new AdminUsageRowDto(window.UserId, window.PlanCode, window.TokensUsed, limit,
                Math.Max(0, limit - window.TokensUsed), window.WindowStart, window.WindowEnd,
                window.Locked || window.TokensUsed >= limit);
        }).ToArray();
        return new PagedDto<AdminUsageRowDto>(items, query.PageIndex, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<IReadOnlyList<TokenLimitDto>> LimitsAsync(CancellationToken ct) =>
        (await repository.ListLimitsAsync(ct))
            .Select(limit => new TokenLimitDto(limit.PlanCode, limit.TokenLimit, limit.UpdatedBy, limit.UpdatedAt)).ToList();

    // Effective immediately for the next chats (limits are read on every request).
    public async Task<TokenLimitDto?> UpdateLimitAsync(Guid adminId, string planCode, long tokenLimit, CancellationToken ct)
    {
        var limit = await repository.GetLimitForUpdateAsync(planCode, ct);
        if (limit is null) return null;
        var now = usage.Now;
        var audit = new AiUsageAudit
        {
            AdminId = adminId, Action = AuditAction.ChangeLimit, PlanCode = planCode, OldValue = limit.TokenLimit,
            NewValue = tokenLimit, CreatedAt = now
        };
        limit.TokenLimit = tokenLimit;
        limit.UpdatedBy = adminId;
        limit.UpdatedAt = now;
        await repository.SaveWithAuditAsync(audit, ct);
        return new TokenLimitDto(limit.PlanCode, limit.TokenLimit, limit.UpdatedBy, limit.UpdatedAt);
    }

    // Reset closes the current window (usage back to 0, unlocked); the next answered chat opens a new one.
    // A request already in flight is charged to the window it started in (the closed one); it never blocks the
    // learner's fresh window.
    public async Task ResetAsync(Guid adminId, Guid userId, CancellationToken ct)
    {
        var now = usage.Now;
        var window = await repository.GetCurrentWindowAsync(userId, now, track: true, ct);
        var audit = new AiUsageAudit
        {
            AdminId = adminId, Action = AuditAction.ResetUsage, UserId = userId, OldValue = window?.TokensUsed ?? 0,
            NewValue = 0, CreatedAt = now
        };
        if (window is not null)
        {
            window.WindowEnd = now;
            window.Locked = false;
            window.UpdatedAt = now;
        }
        await repository.SaveWithAuditAsync(audit, ct);
    }
}
