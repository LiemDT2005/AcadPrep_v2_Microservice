namespace Exam.DTOs;

public class AttemptReviewDto
{
    public bool IsPractice { get; set; }
    public int AttemptOrSessionId { get; set; }
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public int TotalScore { get; set; }
    public int MaxScore { get; set; }
    public int ListeningScore { get; set; }
    public int ReadingScore { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int UnansweredCount { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<ReviewQuestionDto> Questions { get; set; } = new();
}

public class ReviewQuestionDto
{
    public int? AudioStartSecond { get; set; }
    public int? AudioEndSecond { get; set; }
    public int? QuestionGroupId { get; set; }
    public int? PassageId { get; set; }
    public List<SessionPassageDto> Passages { get; set; } = [];
    public int QuestionId { get; set; }
    public int QuestionNumber { get; set; }
    public int Part { get; set; }
    public string? QuestionText { get; set; }
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
    public string? PassageContent { get; set; }
    public string? PassageImageUrl { get; set; }
    public string? TopicTag { get; set; }
    public string? SelectedOption { get; set; }
    public string CorrectOption { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public bool IsAnswered { get; set; }
    public string? Explanation { get; set; }
    public List<ReviewOptionDto> Options { get; set; } = new();
}

public class ReviewOptionDto
{
    public string Letter { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class StudyHistoryResultDto
{
    public List<StudyHistoryItemDto> Items { get; set; } = new();
}

public class StudyHistoryItemDto
{
    public string ActivityType { get; set; } = string.Empty; // exam | practice | vocab
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? LinkUrl { get; set; }
    public string Icon { get; set; } = "history";
    public string ColorType { get; set; } = "primary";
}

public class ExamAttemptsResultDto
{
    public List<ExamAttemptListItemDto> Attempts { get; set; } = new();
}

public class ExamAttemptListItemDto
{
    public int AttemptId { get; set; }
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public int ListeningScore { get; set; }
    public int ReadingScore { get; set; }
    public int TotalScore { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class IncorrectAnswersResultDto
{
    public int TotalIncorrect { get; set; }
    public List<IncorrectAnswerGroupDto> Groups { get; set; } = new();
}

public class IncorrectAnswerGroupDto
{
    public string GroupKey { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public List<IncorrectAnswerItemDto> Items { get; set; } = new();
}

public class IncorrectAnswerItemDto
{
    public bool IsPractice { get; set; }
    public int QuestionId { get; set; }
    public int QuestionNumber { get; set; }
    public int Part { get; set; }
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public int AttemptId { get; set; }
    public string? QuestionText { get; set; }
    public string? TopicTag { get; set; }
    public string? SelectedOption { get; set; }
    public string CorrectOption { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public DateTime? AttemptedAt { get; set; }
    public List<IncorrectOptionDto> Options { get; set; } = new();
}

public class IncorrectOptionDto
{
    public string Letter { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
