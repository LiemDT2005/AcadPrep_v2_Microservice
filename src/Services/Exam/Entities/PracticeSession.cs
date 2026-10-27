using Exam.Common;

namespace Exam.Entities;

public class PracticeSession : BaseEntity<int>
{
    public string UserId { get; set; } = string.Empty;
    public int ExamId { get; set; }
    public string SelectedParts { get; set; } = null!;
    public string? SelectedTags { get; set; }
    public int? TimeLimit { get; set; }
    public string CombinedQuestionsList { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? AnswersJson { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public int ListeningCorrect { get; set; }
    public int ReadingCorrect { get; set; }
    public int ListeningTotal { get; set; }
    public int ReadingTotal { get; set; }

    public virtual Exam Exam { get; set; } = null!;
}
