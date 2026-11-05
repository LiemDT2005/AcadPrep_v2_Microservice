using Ai.API.DTOs;
using Ai.API.Repositories.Interfaces;
using Ai.API.Services.Interfaces;

namespace Ai.API.Services;

// Session list/history are always scoped to the caller; someone else's session is indistinguishable from a missing one.
public class SessionService(IChatSessionRepository sessions, TimeProvider clock) : ISessionService
{
    public async Task<PagedDto<SessionDto>> ListAsync(Guid userId, PageQuery query, CancellationToken ct)
    {
        var (items, total) = await sessions.ListOwnedAsync(userId, query.PageIndex, query.PageSize, ct);
        return Page(items.Select(item => new SessionDto(item.Id, item.Title, item.CreatedAt, item.UpdatedAt)).ToList(),
            query, total);
    }

    public async Task<PagedDto<MessageDto>?> MessagesAsync(Guid userId, Guid sessionId, PageQuery query, CancellationToken ct)
    {
        if (!await sessions.ExistsOwnedAsync(userId, sessionId, ct)) return null;
        var (items, total) = await sessions.ListMessagesAsync(sessionId, query.PageIndex, query.PageSize, ct);
        return Page(items.Select(item => new MessageDto(item.Id, item.Role, item.Content, item.IsOutOfScope, item.CreatedAt))
            .ToList(), query, total);
    }

    // Soft delete: usage already consumed is never refunded and the 7-day history keeps it.
    public Task<bool> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        sessions.SoftDeleteAsync(userId, sessionId, clock.GetUtcNow().UtcDateTime, ct);

    private static PagedDto<T> Page<T>(List<T> items, PageQuery query, int total) =>
        new(items, query.PageIndex, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
}
