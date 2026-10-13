namespace Learning.Domain.Entities;

public class UserAchievement
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int AchievementId { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    public bool IsNotified { get; set; } = false;

    // Navigation property
    public Achievement? Achievement { get; set; }
}
