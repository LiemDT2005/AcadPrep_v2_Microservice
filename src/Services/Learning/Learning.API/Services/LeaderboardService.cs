using BuildingBlocks.Common.Results;
using Learning.API.Caching;
using Learning.API.Data;
using Learning.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Services;

public class LeaderboardService(ILearningDbContext db, ILearningCache cache)
{
    public async Task<Result<LeaderboardResultDto>> GetLeaderboardAsync(
        string currentUserId, string? sortBy = "Score", CancellationToken ct = default)
    {
        var cacheKey = $"leaderboard_{sortBy?.ToLowerInvariant() ?? "score"}";
        var cached = await cache.GetAsync<LeaderboardResultDto>(cacheKey, ct);
        if (cached is not null)
        {
            var entriesWithCurrent = cached.Entries.Select(e =>
                e with { IsCurrentUser = e.UserId == currentUserId }).ToList();
            var currentUserEntry = entriesWithCurrent.FirstOrDefault(e => e.IsCurrentUser);
            return Result.Success(new LeaderboardResultDto(entriesWithCurrent, currentUserEntry, cached.SortBy));
        }

        var users = await db.UserProfiles.AsNoTracking().ToListAsync(ct);
        var streaks = await db.StudyStreaks.AsNoTracking().ToDictionaryAsync(s => s.UserId, ct);

        var scores = await db.ExamAttemptRecords.AsNoTracking()
            .GroupBy(ea => ea.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalScore = g.Sum(x => x.Score),
                ExamsDone = g.Count()
            })
            .ToDictionaryAsync(x => x.UserId, ct);

        var entries = new List<LeaderboardEntryDto>();
        foreach (var u in users)
        {
            scores.TryGetValue(u.UserId, out var userScore);
            streaks.TryGetValue(u.UserId, out var userStreak);

            entries.Add(new LeaderboardEntryDto(
                0,
                u.UserId,
                u.FullName,
                userScore?.TotalScore ?? 0,
                userScore?.ExamsDone ?? 0,
                userStreak?.CurrentStreak ?? 0,
                u.UserId == currentUserId
            ));
        }

        bool byStreak = string.Equals(sortBy, "Streak", StringComparison.OrdinalIgnoreCase);
        entries = byStreak
            ? entries.OrderByDescending(e => e.StreakDays).ThenByDescending(e => e.TotalScore).ToList()
            : entries.OrderByDescending(e => e.TotalScore).ThenByDescending(e => e.StreakDays).ToList();

        for (int i = 0; i < entries.Count; i++)
        {
            entries[i] = entries[i] with { Rank = i + 1 };
        }

        var result = new LeaderboardResultDto(
            entries,
            entries.FirstOrDefault(e => e.IsCurrentUser),
            byStreak ? "Streak" : "Score"
        );

        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), ct);
        return Result.Success(result);
    }
}
