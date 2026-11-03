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
    List<QuestionOptionDto> Options
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
