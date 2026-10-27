namespace Exam.Entities;

// A frozen copy of the paper and the learner's checkpoint. Never returned directly by an API.
public class ExamSessionState
{
    public string Id { get; set; } = string.Empty;
    public string Revision { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string QuestionsJson { get; set; } = "[]";
    public string AnswersJson { get; set; } = "{}";
    public string AudioJson { get; set; } = "{}";
    public DateTime StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ListeningScore { get; set; }
    public int ReadingScore { get; set; }
    public string? SubmissionEventJson { get; set; }
    public bool EventPublished { get; set; }
}
