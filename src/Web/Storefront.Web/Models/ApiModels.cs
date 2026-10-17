namespace Storefront.Web.Models;

public record AuthResponse(string Token, string Email, string FullName, string UserId, DateTime ExpiresAt);
public record CourseListItem(Guid Id, string Title, string Level, int TargetScore, int LessonCount, string Description);
public record LessonDto(Guid Id, string Title, string Skill, int Order, int DurationMinutes, string Content);
public record CourseDetail(Guid Id, string Title, string Description, string Level, int TargetScore, List<LessonDto> Lessons);
public record ExamListItem(Guid Id, string Title, string Part, string Description, int TimeLimitMinutes, int QuestionCount);
public record QuestionDto(Guid Id, string Stem, string OptionA, string OptionB, string OptionC, string OptionD, int Order);
public record ExamDetail(Guid Id, string Title, string Part, string Description, int TimeLimitMinutes, List<QuestionDto> Questions);
public record SubmitExamResult(Guid AttemptId, int Score, int CorrectAnswers, int TotalQuestions, string ExamTitle);
public record LearningRecordDto(Guid Id, string ExamTitle, int Score, int CorrectAnswers, int TotalQuestions, DateTime CompletedAt);
public record ProgressSummary(string UserId, int TotalAttempts, double AverageScore, int BestScore, List<LearningRecordDto> Recent);

public class PaginatedList<T>
{
    public List<T> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int TotalPage { get; set; }
    public int TotalCount { get; set; }

    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPage;
}

public class GetExamDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? CoverImageUrl { get; set; }
    public int QuestionCount { get; set; }
    public int AttemptCount { get; set; }
}

public class GetExamListResponse
{
    public PaginatedList<GetExamDto> Exams { get; set; } = new();
    public List<string> SeriesFilters { get; set; } = new();
    public List<string> YearFilters { get; set; } = new();
}

public class GetExamDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? CoverImageUrl { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalAttempts { get; set; }
    public List<PartSummaryDto> Parts { get; set; } = new();
    public List<UserAttemptHistoryDto> AttemptHistory { get; set; } = new();
}

public class PartSummaryDto
{
    public int PartNumber { get; set; }
    public string PartName { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public List<string> Tags { get; set; } = new();
}

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
