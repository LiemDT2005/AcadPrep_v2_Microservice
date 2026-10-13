namespace Learning.Domain.Entities;

public class StudyStreak
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CurrentStreak { get; set; }
    public int MaxStreak { get; set; }
    public DateOnly LastActiveDate { get; set; }
}
