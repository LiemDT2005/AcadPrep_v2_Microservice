using Ai.API.Entities;

namespace Ai.API.Repositories.Interfaces;

// Sessions and their messages. Every user-facing read is scoped to the owner.
public interface IChatSessionRepository
{
    // Tracked: the chat flow updates UpdatedAt in the same unit of work.
    Task<AiChatSession?> GetOwnedForUpdateAsync(Guid userId, Guid sessionId, CancellationToken ct);
    Task<AiChatSession> GetAsync(Guid sessionId, CancellationToken ct);
    Task<bool> ExistsOwnedAsync(Guid userId, Guid sessionId, CancellationToken ct);
    // UpdatedAt desc, then Id desc.
    Task<(IReadOnlyList<AiChatSession> Items, int Total)> ListOwnedAsync(Guid userId, int pageIndex, int pageSize,
        CancellationToken ct);
    Task<bool> SoftDeleteAsync(Guid userId, Guid sessionId, DateTime now, CancellationToken ct);

    // Chronological (CreatedAt, then Id).
    Task<(IReadOnlyList<AiChatMessage> Items, int Total)> ListMessagesAsync(Guid sessionId, int pageIndex, int pageSize,
        CancellationToken ct);
    // Newest first, at most `take` messages.
    Task<IReadOnlyList<AiChatMessage>> ListRecentMessagesAsync(Guid sessionId, int take, CancellationToken ct);
    Task<AiChatMessage> GetMessageAsync(Guid messageId, CancellationToken ct);
}
