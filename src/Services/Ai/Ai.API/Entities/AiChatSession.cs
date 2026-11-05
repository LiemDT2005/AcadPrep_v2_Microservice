namespace Ai.API.Entities;

public class AiChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Soft delete only: token usage history is kept in AiChatRequests.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
