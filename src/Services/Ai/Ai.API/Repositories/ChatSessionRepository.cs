using Ai.API.Data;
using Ai.API.Entities;
using Ai.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ai.API.Repositories;

public class ChatSessionRepository(AiDbContext db) : IChatSessionRepository
{
    public Task<AiChatSession?> GetOwnedForUpdateAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        db.Sessions.FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId && !item.IsDeleted, ct);

    public Task<AiChatSession> GetAsync(Guid sessionId, CancellationToken ct) =>
        db.Sessions.AsNoTracking().FirstAsync(item => item.Id == sessionId, ct);

    public Task<bool> ExistsOwnedAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        db.Sessions.AnyAsync(item => item.Id == sessionId && item.UserId == userId && !item.IsDeleted, ct);

    public async Task<(IReadOnlyList<AiChatSession> Items, int Total)> ListOwnedAsync(Guid userId, int pageIndex,
        int pageSize, CancellationToken ct)
    {
        var sessions = db.Sessions.AsNoTracking().Where(item => item.UserId == userId && !item.IsDeleted);
        var total = await sessions.CountAsync(ct);
        var items = await sessions.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> SoftDeleteAsync(Guid userId, Guid sessionId, DateTime now, CancellationToken ct)
    {
        DateTime? deletedAt = now;
        return await db.Sessions.Where(item => item.Id == sessionId && item.UserId == userId && !item.IsDeleted)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.IsDeleted, true)
                .SetProperty(item => item.DeletedAt, deletedAt), ct) > 0;
    }

    public async Task<(IReadOnlyList<AiChatMessage> Items, int Total)> ListMessagesAsync(Guid sessionId, int pageIndex,
        int pageSize, CancellationToken ct)
    {
        var messages = db.Messages.AsNoTracking().Where(item => item.SessionId == sessionId);
        var total = await messages.CountAsync(ct);
        var items = await messages.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<AiChatMessage>> ListRecentMessagesAsync(Guid sessionId, int take, CancellationToken ct) =>
        await db.Messages.AsNoTracking().Where(item => item.SessionId == sessionId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Take(Math.Max(0, take)).ToListAsync(ct);

    public Task<AiChatMessage> GetMessageAsync(Guid messageId, CancellationToken ct) =>
        db.Messages.AsNoTracking().FirstAsync(item => item.Id == messageId, ct);
}
