namespace Storefront.Web.Models;

public record AuthResponse(
    string Token,
    string Email,
    string FullName,
    string UserId,
    DateTime ExpiresAt,
    List<string>? Roles = null,
    string? Avatar = null,
    string? Status = null,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAt = null);

public record RegisterResponse(
    string Message,
    string Email,
    string UserId,
    string Status);

public record UserProfileDto(
    string Id,
    string Email,
    string FullName,
    string? Avatar,
    string Status,
    List<string> Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt);
public record CourseListItem(Guid Id, string Title, string Level, int TargetScore, int LessonCount, string Description);
public record LessonDto(Guid Id, string Title, string Skill, int Order, int DurationMinutes, string Content);
public record CourseDetail(Guid Id, string Title, string Description, string Level, int TargetScore, List<LessonDto> Lessons);
public record ExamListItem(Guid Id, string Title, string Part, string Description, int TimeLimitMinutes, int QuestionCount);
public record QuestionDto(Guid Id, string Stem, string OptionA, string OptionB, string OptionC, string OptionD, int Order);
public record ExamDetail(Guid Id, string Title, string Part, string Description, int TimeLimitMinutes, List<QuestionDto> Questions);
public record SubmitExamResult(Guid AttemptId, int Score, int CorrectAnswers, int TotalQuestions, string ExamTitle);
public record LearningRecordDto(Guid Id, string ExamTitle, int Score, int CorrectAnswers, int TotalQuestions, DateTime CompletedAt);
public record ProgressSummary(string UserId, int TotalAttempts, double AverageScore, int BestScore, List<LearningRecordDto> Recent);

// ==========================================
// CONTENT CMS MODELS (UC-16.1 to UC-16.7)
// ==========================================

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record DeleteExamApiResponse(
    string Message,
    bool SoftDeleted
);

public record AdminExamListItem(
    int Id,
    string Title,
    string ExamType,
    string? Description,
    int Duration,
    string? AudioUrl,
    string Status,
    bool IsDeleted,
    DateTime CreatedAt,
    int AttemptCount,
    int QuestionCount,
    int ExamSeriesId,
    string? ExamSeriesName
);

public record AdminQuestionOption(
    int Id,
    string OptionLetter,
    string OptionText
);

public record AdminQuestionListItem(
    int Id,
    int QuestionNumber,
    int Part,
    string? QuestionText,
    string CorrectOption,
    string? AudioUrl,
    string? ImageUrl,
    string? QuestionType,
    string? TopicTag,
    int ExamId,
    string? ExamTitle,
    int? PassageId,
    int OptionsCount
);

public record AdminQuestionDetail(
    int Id,
    int QuestionNumber,
    int Part,
    string? QuestionText,
    string CorrectOption,
    string? Explanation,
    string? AudioUrl,
    int? AudioStartSecond,
    int? AudioEndSecond,
    string? ImageUrl,
    string? QuestionType,
    string? TopicTag,
    int ExamId,
    string? ExamTitle,
    int? PassageId,
    string? PassageContent,
    int? QuestionGroupId,
    string? QuestionGroupName,
    List<AdminQuestionOption> Options
);

public record AdminPassageListItem(
    int Id,
    int ExamId,
    string? ExamTitle,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder,
    int QuestionCount,
    int? QuestionGroupId
);

public record AdminPassageDetail(
    int Id,
    int ExamId,
    string? ExamTitle,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder,
    int? QuestionGroupId,
    List<AdminQuestionListItem> Questions
);

public record AdminQuestionGroupDetail(
    int Id,
    string Name,
    string? AudioUrl,
    int? AudioStartSecond,
    int? AudioEndSecond,
    string? ImageUrl,
    string? Explanation,
    int ExamId,
    List<AdminPassageDetail> Passages,
    List<AdminQuestionDetail> Questions
);

public record AdminExamDetail(
    int Id,
    string Title,
    string ExamType,
    string? Description,
    int Duration,
    string? AudioUrl,
    string Status,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? LastModifiedAt,
    int AttemptCount,
    int ExamSeriesId,
    string? ExamSeriesName,
    List<AdminQuestionDetail> Questions,
    List<AdminPassageDetail> Passages,
    List<AdminQuestionGroupDetail> QuestionGroups
);

public record AdminExamSeries(
    int Id,
    string Name,
    int Year
);

public record AdminCreateExamRequest(
    string Title,
    string? ExamType,
    string? Description,
    int Duration,
    int ExamSeriesId,
    string? AudioUrl
);

public record AdminUpdateExamRequest(
    string Title,
    string? ExamType,
    string? Description,
    int Duration,
    int ExamSeriesId,
    string? AudioUrl,
    string? Status
);

public record AdminChangeExamStatusRequest(
    string Status
);
