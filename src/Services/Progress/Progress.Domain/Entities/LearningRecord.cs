namespace Progress.Domain.Entities;

public class LearningRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid ExamId { get; set; }
    public Guid AttemptId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public int Score { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}
