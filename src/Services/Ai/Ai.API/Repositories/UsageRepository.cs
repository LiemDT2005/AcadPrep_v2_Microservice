using Ai.API.Data;
using Ai.API.Entities;
using Ai.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ai.API.Repositories;

public class UsageRepository(AiDbContext db) : IUsageRepository
{
    public Task<AiUsageWindow?> GetCurrentWindowAsync(Guid userId, DateTime now, bool track, CancellationToken ct)
    {
        var windows = track ? db.UsageWindows : db.UsageWindows.AsNoTracking();
        return windows.Where(window => window.UserId == userId && window.WindowEnd > now)
            .OrderByDescending(window => window.WindowStart).FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<AiUsageWindow> Items, int Total)> ListActiveWindowsAsync(DateTime now, Guid? userId,
        int pageIndex, int pageSize, CancellationToken ct)
    {
        var windows = db.UsageWindows.AsNoTracking().Where(window => window.WindowEnd > now);
        if (userId is { } id) windows = windows.Where(window => window.UserId == id);
        var total = await windows.CountAsync(ct);
        var items = await windows.OrderByDescending(window => window.TokensUsed).ThenBy(window => window.UserId)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<AiTokenLimit?> GetLimitAsync(string planCode, CancellationToken ct) =>
        db.TokenLimits.AsNoTracking().FirstOrDefaultAsync(limit => limit.PlanCode == planCode, ct);

    public Task<AiTokenLimit?> GetLimitForUpdateAsync(string planCode, CancellationToken ct) =>
        db.TokenLimits.FirstOrDefaultAsync(limit => limit.PlanCode == planCode, ct);

    public async Task<IReadOnlyList<AiTokenLimit>> ListLimitsAsync(CancellationToken ct) =>
        await db.TokenLimits.AsNoTracking().OrderBy(limit => limit.TokenLimit).ThenBy(limit => limit.PlanCode).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, long>> GetLimitMapAsync(CancellationToken ct) =>
        await db.TokenLimits.AsNoTracking().ToDictionaryAsync(limit => limit.PlanCode, limit => limit.TokenLimit, ct);

    public async Task SaveWithAuditAsync(AiUsageAudit audit, CancellationToken ct)
    {
        db.Audits.Add(audit);
        await db.SaveChangesAsync(ct);
    }
}
