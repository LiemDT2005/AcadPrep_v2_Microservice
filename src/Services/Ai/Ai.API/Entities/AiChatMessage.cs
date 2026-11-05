namespace Ai.API.Entities;

public class AiChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public required string Role { get; set; }
    public required string Content { get; set; }
    public int? TotalTokens { get; set; }
    public bool IsOutOfScope { get; set; }
    public DateTime CreatedAt { get; set; }
}
