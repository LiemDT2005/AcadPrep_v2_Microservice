using BuildingBlocks.Common.Results;
using Learning.Application.DTOs;
using Learning.Application.Interfaces;
using Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.Application.Services;

public class GamificationService(ILearningDbContext db, ILearningCache cache)
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public static DateOnly GetCurrentDateVietnam()
    {
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        return DateOnly.FromDateTime(localTime);
    }

    // UC-13.1 View Achievements
    public async Task<Result<List<AchievementDto>>> GetAchievementsAsync(
        string userId, CancellationToken ct = default)
    {
        var allAchievements = await db.Achievements.AsNoTracking().ToListAsync(ct);
        var userAchievements = await db.UserAchievements.AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .ToDictionaryAsync(ua => ua.AchievementId, ua => ua.UnlockedAt, ct);

        var list = allAchievements.Select(a =>
        {
            var isUnlocked = userAchievements.ContainsKey(a.Id);
            return new AchievementDto(
                a.Id,
                a.Name,
                a.Description,
                a.IconUrl,
                a.ConditionType,
                a.ConditionValue,
                isUnlocked,
                isUnlocked ? userAchievements[a.Id] : null
            );
        })
        .OrderByDescending(a => a.IsUnlocked)
        .ThenBy(a => a.AchievementId)
        .ToList();

        return Result.Success(list);
    }

    // UC-13.2 View Study Streak
    public async Task<Result<StudyStreakDto>> GetStudyStreakAsync(
        string userId, CancellationToken ct = default)
    {
        var streak = await db.StudyStreaks
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        var today = GetCurrentDateVietnam();
        var yesterday = today.AddDays(-1);

        if (streak is null)
        {
            return Result.Success(new StudyStreakDto(0, 0, today, false));
        }

        var currentStreak = streak.CurrentStreak;
        // In-memory adjustment if user missed yesterday in UTC+7
        if (streak.LastActiveDate < yesterday)
        {
            currentStreak = 0;
        }

        var isActiveToday = streak.LastActiveDate == today;

        return Result.Success(new StudyStreakDto(
            currentStreak,
            streak.MaxStreak,
            streak.LastActiveDate,
            isActiveToday
        ));
    }

    // Process TestSubmitted: Update Streak + Grant Achievements
    public async Task OnTestSubmittedAsync(
        string userId, Guid attemptId, Guid examId, string examTitle,
        int score, int listeningScore, int readingScore,
        int totalQuestions, int correctAnswers,
        Dictionary<int, int>? partBreakdown = null,
        CancellationToken ct = default)
    {
        // 1. Record Exam Attempt
        var exists = await db.ExamAttemptRecords.AnyAsync(x => x.AttemptId == attemptId, ct);
        if (!exists)
        {
            var attemptRecord = new ExamAttemptRecord
            {
                AttemptId = attemptId,
                UserId = userId,
                ExamId = examId,
                ExamTitle = examTitle,
                Score = score,
                ListeningScore = listeningScore,
                ReadingScore = readingScore,
                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                ListeningCorrect = (listeningScore > 0 && totalQuestions > 0) ? (int)Math.Round(listeningScore / 4.95) : 0,
                ReadingCorrect = (readingScore > 0 && totalQuestions > 0) ? (int)Math.Round(readingScore / 4.95) : 0,
                PartMasteryJson = partBreakdown is not null ? System.Text.Json.JsonSerializer.Serialize(partBreakdown) : null,
                SubmittedAt = DateTime.UtcNow
            };
            db.ExamAttemptRecords.Add(attemptRecord);
        }

        // 2. Update Study Streak (UTC+7 timezone logic)
        var today = GetCurrentDateVietnam();
        var yesterday = today.AddDays(-1);

        var streak = await db.StudyStreaks.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (streak is null)
        {
            streak = new StudyStreak
            {
                UserId = userId,
                CurrentStreak = 1,
                MaxStreak = 1,
                LastActiveDate = today
            };
            db.StudyStreaks.Add(streak);
        }
        else
        {
            if (streak.LastActiveDate == yesterday)
            {
                streak.CurrentStreak++;
                if (streak.CurrentStreak > streak.MaxStreak)
                {
                    streak.MaxStreak = streak.CurrentStreak;
                }
                streak.LastActiveDate = today;
            }
            else if (streak.LastActiveDate < yesterday)
            {
                streak.CurrentStreak = 1;
                streak.LastActiveDate = today;
            }
            // If already today, streak continues without incrementing count
        }

        await db.SaveChangesAsync(ct);

        // 3. Evaluate and grant achievements
        await CheckAndGrantAchievementsAsync(userId, ct);

        // 4. Invalidate caches
        await cache.InvalidateUserAsync(userId, ct);
        await cache.InvalidateAdminReportsAsync(ct);
    }

    public async Task<List<Achievement>> CheckAndGrantAchievementsAsync(
        string userId, CancellationToken ct = default)
    {
        var examsCount = await db.ExamAttemptRecords
            .Where(x => x.UserId == userId)
            .CountAsync(ct);

        var maxScore = await db.ExamAttemptRecords
            .Where(x => x.UserId == userId)
            .MaxAsync(x => (int?)x.Score, ct) ?? 0;

        var vocabCount = await db.SavedVocabularies
            .Where(x => x.UserId == userId)
            .CountAsync(ct);

        var streakEntity = await db.StudyStreaks
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var maxStreak = streakEntity?.MaxStreak ?? 0;

        var unlockedIds = await db.UserAchievements
            .Where(x => x.UserId == userId)
            .Select(x => x.AchievementId)
            .ToListAsync(ct);

        var locked = await db.Achievements
            .Where(a => !unlockedIds.Contains(a.Id))
            .ToListAsync(ct);

        var newlyGranted = new List<Achievement>();
        foreach (var achievement in locked)
        {
            var isGranted = achievement.ConditionType switch
            {
                "ExamsCompleted" => examsCount >= achievement.ConditionValue,
                "Score" => maxScore >= achievement.ConditionValue,
                "Streak" => maxStreak >= achievement.ConditionValue,
                "VocabCount" => vocabCount >= achievement.ConditionValue,
                _ => false
            };

            if (isGranted)
            {
                db.UserAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = achievement.Id,
                    UnlockedAt = DateTime.UtcNow,
                    IsNotified = false
                });
                newlyGranted.Add(achievement);
            }
        }

        if (newlyGranted.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return newlyGranted;
    }

    // Daily streak reset check (resets streaks older than yesterday)
    public async Task ResetExpiredStreaksAsync(CancellationToken ct = default)
    {
        var today = GetCurrentDateVietnam();
        var yesterday = today.AddDays(-1);

        var expiredStreaks = await db.StudyStreaks
            .Where(s => s.LastActiveDate < yesterday && s.CurrentStreak > 0)
            .ToListAsync(ct);

        foreach (var s in expiredStreaks)
        {
            s.CurrentStreak = 0;
        }

        if (expiredStreaks.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    // Admin CRUD for Achievements
    public async Task<Result<AchievementDto>> CreateAchievementAsync(
        CreateAchievementRequest request, CancellationToken ct = default)
    {
        var entity = new Achievement
        {
            Name = request.Name,
            Description = request.Description,
            IconUrl = request.IconUrl,
            ConditionType = request.ConditionType,
            ConditionValue = request.ConditionValue
        };
        db.Achievements.Add(entity);
        await db.SaveChangesAsync(ct);

        return Result.Success(new AchievementDto(
            entity.Id, entity.Name, entity.Description, entity.IconUrl,
            entity.ConditionType, entity.ConditionValue, false, null
        ));
    }

    public async Task<Result<AchievementDto>> UpdateAchievementAsync(
        int id, UpdateAchievementRequest request, CancellationToken ct = default)
    {
        var entity = await db.Achievements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return Result.Failure<AchievementDto>("Không tìm thấy danh hiệu.");

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.IconUrl = request.IconUrl;
        entity.ConditionType = request.ConditionType;
        entity.ConditionValue = request.ConditionValue;

        await db.SaveChangesAsync(ct);

        return Result.Success(new AchievementDto(
            entity.Id, entity.Name, entity.Description, entity.IconUrl,
            entity.ConditionType, entity.ConditionValue, false, null
        ));
    }

    public async Task<Result<bool>> DeleteAchievementAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Achievements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return Result.Failure<bool>("Không tìm thấy danh hiệu.");

        db.Achievements.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
