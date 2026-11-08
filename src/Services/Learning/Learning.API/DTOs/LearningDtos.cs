using System.ComponentModel.DataAnnotations;
using Learning.API.Validation;

namespace Learning.API.DTOs;

// ==================== VOCABULARY DTOs ====================

public record SavedVocabularyDto(
    int VocabularyId,
    string Word,
    string? Phonetic,
    string Meaning,
    string? Example,
    int Interval,
    DateTime NextReviewDate,
    DateTime DateSaved
);

public record VocabPassageDto(int Id, string Content, int VocabularyId = 0);

public record VocabContextDto(
    int VocabularyId,
    string Word,
    string? Phonetic,
    string Meaning,
    string? Example,
    List<VocabPassageDto> Passages
);

public record VocabularyDetailDto(
    int Id,
    string Word,
    string? Phonetic,
    string Meaning,
    string? Example
)
{
    public int VocabularyId => Id;
}

public record SaveVocabularyRequest(
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "VocabularyId phai la so nguyen duong.")]
    int VocabularyId
);

public record SaveVocabularyResponse(
    int VocabularyId,
    string Word,
    string Meaning,
    DateTime DateSaved,
    DateTime NextReviewDate
);

public record RateVocabularyRequest(
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "VocabularyId phai la so nguyen duong.")]
    int VocabularyId,
    [Required]
    bool IsRemembered
);

public record RateVocabularyResponse(
    int VocabularyId,
    int NextIntervalDays,
    DateTime NextReviewDate,
    string Message
);

// ==================== GAMIFICATION DTOs ====================

public record AchievementDto(
    int AchievementId,
    string Name,
    string Description,
    string? IconUrl,
    string ConditionType,
    int ConditionValue,
    bool IsUnlocked,
    DateTime? UnlockedAt
);

public record StudyStreakDto(
    int CurrentStreak,
    int MaxStreak,
    DateOnly LastActiveDate,
    bool IsActiveToday
);

public record CreateAchievementRequest(
    [Required(ErrorMessage = "Ten danh hieu khong duoc de trong.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Ten danh hieu phai tu 1 den 200 ky tu.")]
    string Name,

    [Required(ErrorMessage = "Mo ta danh hieu khong duoc de trong.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Mo ta danh hieu phai tu 1 den 500 ky tu.")]
    string Description,

    [MaxLength(500, ErrorMessage = "Duong dan Icon khong duoc vuot qua 500 ky tu.")]
    string? IconUrl,

    [Required(ErrorMessage = "Loai dieu kien khong duoc de trong.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Loai dieu kien phai tu 1 den 100 ky tu.")]
    [ValidAchievementCondition]
    string ConditionType,

    [Required(ErrorMessage = "Gia tri dieu kien khong duoc de trong.")]
    [Range(1, int.MaxValue, ErrorMessage = "Gia tri dieu kien phai lon hon 0.")]
    int ConditionValue
);

public record UpdateAchievementRequest(
    [Required(ErrorMessage = "Ten danh hieu khong duoc de trong.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Ten danh hieu phai tu 1 den 200 ky tu.")]
    string Name,

    [Required(ErrorMessage = "Mo ta danh hieu khong duoc de trong.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Mo ta danh hieu phai tu 1 den 500 ky tu.")]
    string Description,

    [MaxLength(500, ErrorMessage = "Duong dan Icon khong duoc vuot qua 500 ky tu.")]
    string? IconUrl,

    [Required(ErrorMessage = "Loai dieu kien khong duoc de trong.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Loai dieu kien phai tu 1 den 100 ky tu.")]
    [ValidAchievementCondition]
    string ConditionType,

    [Required(ErrorMessage = "Gia tri dieu kien khong duoc de trong.")]
    [Range(1, int.MaxValue, ErrorMessage = "Gia tri dieu kien phai lon hon 0.")]
    int ConditionValue
);

// ==================== LEADERBOARD DTOs ====================

public record LeaderboardEntryDto(
    int Rank,
    string UserId,
    string Name,
    int TotalScore,
    int ExamsDone,
    int StreakDays,
    bool IsCurrentUser = false
);

public record LeaderboardResultDto(
    List<LeaderboardEntryDto> Entries,
    LeaderboardEntryDto? CurrentUserEntry,
    string SortBy
);

// ==================== PERFORMANCE DTOs ====================

public record PerformanceAnalysisDto(
    bool HasData,
    string Message,
    double AvgListeningScore,
    double AvgReadingScore,
    int TotalExamsTaken,
    Dictionary<int, int> PartMastery
)
{
    public double AverageListeningScore => AvgListeningScore;
    public double AverageReadingScore => AvgReadingScore;
    public int TotalTestsTaken => TotalExamsTaken;
}

public record ScoreProgressItemDto(
    DateTime SubmittedAt,
    int Score,
    string ExamTitle
)
{
    public DateTime AttemptDate => SubmittedAt;
}

public record ScoreProgressResultDto(
    bool HasSufficientData,
    string Message,
    List<ScoreProgressItemDto> Scores
);

public record StudyActivityDto(
    string ActivityType,
    string Title,
    string Description,
    DateTime OccurredAt,
    string LinkUrl,
    string Icon,
    string ColorType
);

public record StudyHistoryDto(List<StudyActivityDto> Items);

// ==================== ADMIN DTOs ====================

public record ProgressDataPointDto(
    DateOnly Date,
    int CompletedExamsCount,
    double AverageScore
);

public record ProgressReportDto(
    List<ProgressDataPointDto> DataPoints,
    int TotalExamsCompleted,
    double OverallAverageScore
);

public record UserStatsDto(
    int TotalRegisteredUsers,
    int NewRegistrationsLast30Days,
    int ActiveUsersLast7Days,
    int TotalExamsTaken,
    double AverageExamScore,
    double RetentionRatePercent
)
{
    public int TotalUsers => TotalRegisteredUsers;
    public int NewRegistrations => NewRegistrationsLast30Days;
    public int ActiveUsers => ActiveUsersLast7Days;
    public double AverageToeicScore => AverageExamScore;
    public double RetentionRate => RetentionRatePercent;
}

public record ExamStatsItemDto(
    Guid ExamId,
    string ExamName,
    int TotalAttempts,
    double AverageScore,
    int HighestScore,
    int LowestScore,
    double CompletionRate
);

public record PaginatedListDto<T>(
    List<T> Items,
    int PageNumber,
    int TotalPages,
    int TotalCount,
    bool HasPreviousPage,
    bool HasNextPage
);

public class AdminReportQueryRequest : IValidatableObject
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var start = StartDate ?? DateTime.UtcNow.AddDays(-30);
        var end = EndDate ?? DateTime.UtcNow;

        if (start.Date > end.Date)
        {
            yield return new ValidationResult(
                "Ngay bat dau (StartDate) khong duoc lon hon ngay ket thuc (EndDate).",
                [nameof(StartDate), nameof(EndDate)]);
        }

        if ((end.Date - start.Date).TotalDays > 365)
        {
            yield return new ValidationResult(
                "Khoang thoi gian truy van bao cao khong duoc vuot qua 365 ngay de toi uu hieu nang he thong.",
                [nameof(StartDate), nameof(EndDate)]);
        }

        if (start.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            yield return new ValidationResult(
                "Ngay bat dau khong duoc nam trong tuong lai.",
                [nameof(StartDate)]);
        }
    }
}

