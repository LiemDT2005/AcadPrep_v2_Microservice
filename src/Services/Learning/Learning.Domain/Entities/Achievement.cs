namespace Learning.Domain.Entities;

public class Achievement
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string ConditionType { get; set; } = string.Empty;
    public int ConditionValue { get; set; }

    // Navigation property
    public List<UserAchievement> UserAchievements { get; set; } = [];
}
