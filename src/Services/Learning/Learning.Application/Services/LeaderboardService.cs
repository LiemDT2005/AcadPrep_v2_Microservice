using BuildingBlocks.Common.Results;
using Learning.Application.DTOs;
using Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learning.Application.Services;

public class LeaderboardService(ILearningDbContext db, ILearningCache cache)
{
    public async Task<Result<LeaderboardResultDto>> GetLeaderboardAsync(
        string currentUserId, string? sortBy = "Score", CancellationToken ct = default)
    {
        var cacheKey = $"leaderboard_{sortBy?.ToLowerInvariant() ?? "score"}";
        var cached = await cache.GetAsync<LeaderboardResultDto>(cacheKey, ct);
        if (cached is not null)
        {
            // Re-mark IsCurrentUser based on currentUserId
            var markedEntries = cached.Entries.Select(e => e with { IsCurrentUser = e.UserId == currentUserId }).ToList();
            var currentUserEntry = markedEntries.FirstOrDefault(e => e.IsCurrentUser);
            return Result.Success(new LeaderboardResultDto(markedEntries, currentUserEntry));
        }

        var users = await db.UserProfiles.AsNoTracking().ToListAsync(ct);
        var attempts = await db.ExamAttemptRecords.AsNoTracking().ToListAsync(ct);
        var streaks = await db.StudyStreaks.AsNoTracking().ToListAsync(ct);

        var streakMap = streaks.ToDictionary(s => s.UserId, s => s.CurrentStreak);
        var scoreMap = attempts.GroupBy(a => a.UserId).ToDictionary(
            g => g.Key,
            g => new { TotalScore = g.Sum(x => x.Score), Count = g.Count() }
        );

        var list = new List<LeaderboardEntryDto>();
        foreach (var user in users)
        {
            var userScore = scoreMap.TryGetValue(user.UserId, out var sc) ? sc.TotalScore : 0;
            var examsDone = sc?.Count ?? 0;
            var streak = streakMap.TryGetValue(user.UserId, out var st) ? st : 0;
            var displayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email;

            list.Add(new LeaderboardEntryDto(
                0,
                user.UserId,
                displayName,
                userScore,
                examsDone,
                streak,
                user.UserId == currentUserId
            ));
        }

        // Apply sorting
        IEnumerable<LeaderboardEntryDto> sorted = string.Equals(sortBy, "Streak", StringComparison.OrdinalIgnoreCase)
            ? list.OrderByDescending(x => x.StreakDays).ThenByDescending(x => x.TotalScore)
            : list.OrderByDescending(x => x.TotalScore).ThenByDescending(x => x.StreakDays);

        var rankedList = sorted.Select((entry, index) => entry with { Rank = index + 1 }).ToList();
        var currentEntry = rankedList.FirstOrDefault(e => e.IsCurrentUser);

        var result = new LeaderboardResultDto(rankedList, currentEntry);
        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), ct);

        return Result.Success(result);
    }
}
