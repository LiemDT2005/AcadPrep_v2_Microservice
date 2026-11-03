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
// CONTENT CMS MODELS (UC-16.x, UC-17.x, UC-18.x)
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

public record ExamStatsSummary(
    int TotalExams,
    int ActiveExams,
    int HiddenExams,
    int TotalAttempts
);

public record QuestionStatsSummary(
    int TotalQuestions,
    int ListeningQuestions,
    int ReadingQuestions,
    int ActiveExamQuestions
);

public record PassageStatsSummary(
    int TotalPassages,
    int Part6Passages,
    int Part7Passages,
    int ActiveExamPassages
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
    string? Explanation,
    string? AudioUrl,
    string? ImageUrl,
    string? QuestionType,
    string? TopicTag,
    string Difficulty,
    int ExamId,
    string? ExamTitle,
    string ExamStatus,
    bool IsExamActive,
    int? PassageId,
    int? QuestionGroupId,
    string? QuestionGroupName,
    int OptionsCount,
    List<AdminQuestionOption> Options
);

public record AdminEmbeddedExamItem(
    int ExamId,
    string ExamTitle,
    string ExamType,
    string Status,
    bool IsActive,
    int Duration,
    string? ExamSeriesName
);

public record AdminQuestionBankDetail(
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
    string Difficulty,
    int ExamId,
    string? ExamTitle,
    string ExamStatus,
    bool IsExamActive,
    int? PassageId,
    string? PassageContent,
    string? PassageImageUrl,
    int? QuestionGroupId,
    string? QuestionGroupName,
    List<AdminQuestionOption> Options,
    List<AdminEmbeddedExamItem> EmbeddedExams
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
    List<AdminQuestionOption> Options,
    string? Difficulty = "Medium",
    string? ExamStatus = "Draft",
    bool IsExamActive = false
);

public record AdminPassageListItem(
    int Id,
    int ExamId,
    string? ExamTitle,
    string ExamStatus,
    bool IsExamActive,
    int Part,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder,
    int? QuestionGroupId,
    string? QuestionGroupName,
    int QuestionCount
);

public record AdminPassageCmsDetail(
    int Id,
    int ExamId,
    string? ExamTitle,
    string ExamStatus,
    bool IsExamActive,
    int Part,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder,
    int? QuestionGroupId,
    string? QuestionGroupName,
    List<AdminQuestionListItem> Questions
);

public record AdminUpsertPassageRequest(
    int ExamId,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder = 1,
    int? QuestionGroupId = null
);

public record DeletePassageApiResponse(
    string Message,
    int DeletedQuestionsCount,
    bool WasInActiveExam,
    string? ExamTitle
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
    List<AdminQuestionListItem>? Questions = null
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


public record AdminUpsertQuestionRequest(
    int ExamId,
    int Part,
    int QuestionNumber,
    string? QuestionText,
    string CorrectOption,
    string? Explanation,
    string? AudioUrl,
    int? AudioStartSecond,
    int? AudioEndSecond,
    string? ImageUrl,
    string? QuestionType,
    string? TopicTag,
    string? Difficulty,
    int? PassageId,
    int? QuestionGroupId,
    string OptionA,
    string OptionB,
    string OptionC,
    string? OptionD
);

public record AdminSaveListeningQuestionRequest(
    int? QuestionId,
    int ExamId,
    int Part,
    int QuestionNumber,
    string? QuestionText,
    string CorrectOption,
    string? Explanation,
    string? AudioUrl,
    int? AudioStartSecond,
    int? AudioEndSecond,
    string? ImageUrl,
    string? Difficulty,
    string OptionA,
    string OptionB,
    string OptionC,
    string? OptionD
);

public record AdminSaveListeningGroupQuestionItem(
    int? QuestionId,
    int QuestionNumber,
    string? QuestionText,
    string CorrectOption,
    string? Explanation,
    string? Difficulty,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD
);

public record AdminSaveListeningGroupRequest(
    int? GroupId,
    int ExamId,
    int Part,
    string Name,
    string? AudioUrl,
    int? AudioStartSecond,
    int? AudioEndSecond,
    string? ImageUrl,
    string? Explanation,
    List<AdminSaveListeningGroupQuestionItem> Questions
);

public record AdminSavePart5QuestionRequest(
    int? QuestionId,
    int ExamId,
    int QuestionNumber,
    string QuestionText,
    string CorrectOption,
    string? Explanation,
    string? QuestionType,
    string? TopicTag,
    string? Difficulty,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD
);

public record AdminSavePart6QuestionItem(
    int? QuestionId,
    int QuestionNumber,
    string? QuestionText,
    string CorrectOption,
    string? Explanation,
    string? Difficulty,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD
);

public record AdminSavePart6SetRequest(
    int? PassageId,
    int ExamId,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder,
    List<AdminSavePart6QuestionItem> Questions
);

public record AdminSavePart7PassageItem(
    int? PassageId,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder
);

public record AdminSavePart7QuestionItem(
    int? QuestionId,
    int QuestionNumber,
    string QuestionText,
    string CorrectOption,
    string? Explanation,
    string? Difficulty,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD
);

public record AdminSavePart7SetRequest(
    int? GroupId,
    int ExamId,
    string Name,
    string? Explanation,
    List<AdminSavePart7PassageItem> Passages,
    List<AdminSavePart7QuestionItem> Questions
);

public record DeleteQuestionApiResponse(
    string Message,
    bool WasInActiveExam,
    string? ExamTitle
);

public record IdApiResponse(
    int Id,
    string? Message
);
