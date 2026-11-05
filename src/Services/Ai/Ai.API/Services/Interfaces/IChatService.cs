using Ai.API.DTOs;

namespace Ai.API.Services.Interfaces;

public interface IChatService
{
    Task<ChatResult> SendAsync(Guid userId, ChatRequestDto dto, CancellationToken ct);
}
