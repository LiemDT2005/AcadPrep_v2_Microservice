using Ai.API.DTOs;

namespace Ai.API.Services.Interfaces;

public interface ISessionService
{
    Task<PagedDto<SessionDto>> ListAsync(Guid userId, PageQuery query, CancellationToken ct);
    // Null when the session is missing, deleted or belongs to someone else.
    Task<PagedDto<MessageDto>?> MessagesAsync(Guid userId, Guid sessionId, PageQuery query, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct);
}
