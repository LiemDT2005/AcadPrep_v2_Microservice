namespace Exam.DTOs;

public class UserAttemptHistoryDto
{
    public int AttemptId { get; set; }
    public int ListeningScore { get; set; }
    public int ReadingScore { get; set; }
    public int TotalScore { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RemainingTime { get; set; }
    public bool IsSubmitted { get; set; }
}
