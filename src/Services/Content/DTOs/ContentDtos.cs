namespace Content.DTOs;

public record QuestionOptionDto(
    int Id,
    string OptionLetter,
    string OptionText
);

public record QuestionDetailDto(
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
    List<QuestionOptionDto> Options,
    string Difficulty = "Medium"
);

public record QuestionListItemDto(
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
    List<QuestionOptionDto> Options
);

public record EmbeddedExamDto(
    int ExamId,
    string ExamTitle,
    string ExamType,
    string Status,
    bool IsActive,
    int Duration,
    string? ExamSeriesName
);

public record QuestionBankDetailDto(
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
    List<QuestionOptionDto> Options,
    List<EmbeddedExamDto> EmbeddedExams
);

public record PassageDetailDto(
    int Id,
    int ExamId,
    string? ExamTitle,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder,
    int? QuestionGroupId
);

public record QuestionGroupDetailDto(
    int Id,
    string Name,
    string? AudioUrl,
    int? AudioStartSecond,
    int? AudioEndSecond,
    string? ImageUrl,
    string? Explanation,
    int ExamId,
    List<PassageDetailDto> Passages,
    List<QuestionDetailDto> Questions
);

public record ExamListItemDto(
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

public record ExamDetailDto(
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
    List<QuestionDetailDto> Questions,
    List<PassageDetailDto> Passages,
    List<QuestionGroupDetailDto> QuestionGroups
);

public record ExamSeriesDto(
    int Id,
    string Name,
    int Year
);

public record CreateExamDto(
    string Title,
    string? ExamType,
    string? Description,
    int Duration,
    int ExamSeriesId,
    string? AudioUrl
);

public record UpdateExamDto(
    string Title,
    string? ExamType,
    string? Description,
    int Duration,
    int ExamSeriesId,
    string? AudioUrl,
    string? Status
);

public record DeleteExamResultDto(
    string Message,
    bool SoftDeleted
);

public record PagedResultDto<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

// ==========================================
// QUESTION & EXAM CONTENT CRUD DTOs (UC-17.1 - UC-17.7)
// ==========================================

public record UpsertQuestionRequestDto(
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

public record SaveListeningQuestionRequestDto(
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

public record SaveListeningGroupQuestionItemDto(
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

public record SaveListeningGroupRequestDto(
    int? GroupId,
    int ExamId,
    int Part,
    string Name,
    string? AudioUrl,
    int? AudioStartSecond,
    int? AudioEndSecond,
    string? ImageUrl,
    string? Explanation,
    List<SaveListeningGroupQuestionItemDto> Questions
);

public record SavePart5QuestionRequestDto(
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

public record SavePart6QuestionItemDto(
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

public record SavePart6SetRequestDto(
    int? PassageId,
    int ExamId,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder,
    List<SavePart6QuestionItemDto> Questions
);

public record SavePart7PassageItemDto(
    int? PassageId,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder
);

public record SavePart7QuestionItemDto(
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

public record SavePart7SetRequestDto(
    int? GroupId,
    int ExamId,
    string Name,
    string? Explanation,
    List<SavePart7PassageItemDto> Passages,
    List<SavePart7QuestionItemDto> Questions
);

public record DeleteQuestionResultDto(
    string Message,
    bool WasInActiveExam,
    string? ExamTitle
);

public record ExamStatsSummaryDto(
    int TotalExams,
    int ActiveExams,
    int HiddenExams,
    int TotalAttempts
);

public record QuestionStatsSummaryDto(
    int TotalQuestions,
    int ListeningQuestions,
    int ReadingQuestions,
    int ActiveExamQuestions
);

// ==========================================
// READING PASSAGES CMS DTOs (UC-18.1 - UC-18.5)
// ==========================================

public record PassageListItemDto(
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

public record PassageCmsDetailDto(
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
    List<QuestionListItemDto> Questions
);

public record UpsertPassageRequestDto(
    int ExamId,
    string? Content,
    string? ImageUrl,
    string? Explanation,
    int DisplayOrder = 1,
    int? QuestionGroupId = null
);

public record DeletePassageResultDto(
    string Message,
    int DeletedQuestionsCount,
    bool WasInActiveExam,
    string? ExamTitle
);

public record PassageStatsSummaryDto(
    int TotalPassages,
    int Part6Passages,
    int Part7Passages,
    int ActiveExamPassages
);
