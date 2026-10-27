namespace Learning.Domain.Entities;

public class ExamAttemptRecord
{
    public Guid AttemptId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public int Score { get; set; }
    public int ListeningScore { get; set; }
    public int ReadingScore { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public int ListeningCorrect { get; set; }
    public int ReadingCorrect { get; set; }
    public string? PartMasteryJson { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
