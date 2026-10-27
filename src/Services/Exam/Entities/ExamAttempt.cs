using Exam.Common;

namespace Exam.Entities;

public class ExamAttempt : BaseEntity<int>
{
    public string UserId { get; set; } = string.Empty;
    public int ExamId { get; set; }
    public int ListeningScore { get; set; }
    public int ReadingScore { get; set; }
    public int TotalScore { get; set; }
    public int RemainingTime { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public virtual Exam Exam { get; set; } = null!;
    public virtual ICollection<AttemptAnswer> AttemptAnswers { get; set; } = new List<AttemptAnswer>();
}
