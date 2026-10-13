namespace Learning.Application.DTOs;

// --- Leaderboard (UC-4) ---
public record LeaderboardEntryDto(
    int Rank,
    string UserId,
    string Name,
    int TotalScore,
    int ExamsDone,
    int StreakDays,
    bool IsCurrentUser
);

public record LeaderboardResultDto(
    List<LeaderboardEntryDto> Entries,
    LeaderboardEntryDto? CurrentUserEntry
);

// --- Performance Analysis & Score Progress (UC-10.2, UC-10.3) ---
public record PerformanceAnalysisDto(
    bool HasData,
    string Message,
    double AverageListeningScore,
    double AverageReadingScore,
    int TotalTestsTaken,
    Dictionary<int, int> PartMastery
);

public record ScoreProgressItemDto(
    DateTime AttemptDate,
    int Score,
    string ExamTitle
);

public record ScoreProgressResultDto(
    bool HasSufficientData,
    string Message,
    List<ScoreProgressItemDto> Scores
);

// --- Vocabulary & Flashcards (UC-11.1 - UC-11.5, UC-12.1, UC-12.2) ---
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

public record VocabPassageDto(
    int Id,
    int VocabularyId,
    string Content
);

public record VocabContextDto(
    int VocabularyId,
    string Word,
    string? Phonetic,
    string Meaning,
    string? Example,
    List<VocabPassageDto> Passages
);

public record VocabularyDetailDto(
    int VocabularyId,
    string Word,
    string? Phonetic,
    string Meaning,
    string? Example
);

public record SaveVocabularyRequest(int VocabularyId);

public record RateVocabularyRequest(int VocabularyId, bool IsRemembered);

public record PaginatedListDto<T>(
    List<T> Items,
    int PageNumber,
    int TotalPages,
    int TotalCount,
    bool HasPreviousPage,
    bool HasNextPage
);

// --- Gamification: Achievements & Study Streak (UC-13.1, UC-13.2) ---
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

public record CreateAchievementRequest(
    string Name,
    string Description,
    string? IconUrl,
    string ConditionType,
    int ConditionValue
);

public record UpdateAchievementRequest(
    string Name,
    string Description,
    string? IconUrl,
    string ConditionType,
    int ConditionValue
);

public record StudyStreakDto(
    int CurrentStreak,
    int MaxStreak,
    DateOnly LastActiveDate,
    bool IsActiveToday
);

// --- Admin Analytics & Reports (UC-20.1, UC-20.2, UC-20.3) ---
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
    int TotalUsers,
    int NewRegistrations,
    int ActiveUsers,
    int TotalExamsTaken,
    double AverageToeicScore,
    double RetentionRate
);

public record ExamStatsItemDto(
    Guid ExamId,
    string ExamName,
    int TotalAttempts,
    double AverageScore,
    int HighestScore,
    int LowestScore,
    double CompletionRate
);
