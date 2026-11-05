using Ai.API.Data;
using Ai.API.Entities;
using Ai.API.Entities.Enums;
using Ai.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ai.API.Repositories;

public class ChatRequestRepository(AiDbContext db) : IChatRequestRepository
{
    public Task<AiChatRequest?> FindAsync(Guid userId, Guid requestId, CancellationToken ct) =>
        db.Requests.FirstOrDefaultAsync(item => item.UserId == userId && item.RequestId == requestId, ct);

    public async Task ExpireLeasesAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        // Values computed outside the expression so EF can translate the bulk update.
        var version = Guid.NewGuid();
        await db.Requests.Where(item => item.UserId == userId && item.Status == ChatRequestStatus.Processing && item.LeaseUntil <= now)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.Status, ChatRequestStatus.Failed)
                .SetProperty(item => item.Error, "LeaseExpired").SetProperty(item => item.Version, version), ct);
    }

    public Task ReloadAsync(AiChatRequest request, CancellationToken ct) => db.Entry(request).ReloadAsync(ct);

    public async Task<bool> TryClaimAsync(AiChatRequest request, bool isNew, CancellationToken ct)
    {
        if (isNew) db.Requests.Add(request);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public Task UpdateAsync(AiChatRequest request, CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var tracked = await db.Requests.FirstAsync(item => item.Id == id, ct);
        tracked.Status = ChatRequestStatus.Failed;
        tracked.Error = error;
        tracked.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(AiChatRequest request, AiChatSession? newSession, IReadOnlyList<AiChatMessage> messages,
        AiUsageWindow? newWindow, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (newSession is not null) db.Sessions.Add(newSession);
        db.Messages.AddRange(messages);
        if (newWindow is not null) db.UsageWindows.Add(newWindow);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<(DateTime CompletedAt, int Tokens)>> ListCompletedTokensSinceAsync(Guid userId,
        DateTime fromUtc, CancellationToken ct)
    {
        var rows = await db.Requests.AsNoTracking()
            .Where(request => request.UserId == userId && request.Status == ChatRequestStatus.Completed &&
                request.CompletedAt >= fromUtc && request.TotalTokens > 0)
            .Select(request => new { request.CompletedAt, request.TotalTokens }).ToListAsync(ct);
        return rows.Select(row => (row.CompletedAt!.Value, row.TotalTokens!.Value)).ToArray();
    }
}
