using BuildingBlocks.Common.Results;
using Learning.API.Caching;
using Learning.API.Data;
using Learning.API.DTOs;
using Learning.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Services;

public class LeaderboardService(ILearningDbContext db, ILearningCache cache)
{
    public async Task<Result<LeaderboardResultDto>> GetLeaderboardAsync(
        string currentUserId, string? sortBy = "Score", string? currentUserName = null, string? currentUserEmail = null, CancellationToken ct = default)
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

        var users = await db.UserProfiles.ToListAsync(ct);

        // Ensure current authenticated user has a profile in UserProfiles if valid
        if (!string.IsNullOrEmpty(currentUserId) && currentUserId != "guest" && !users.Any(u => u.UserId == currentUserId))
        {
            var newProfile = new UserProfileSnapshot
            {
                UserId = currentUserId,
                FullName = string.IsNullOrWhiteSpace(currentUserName) ? "Default Learner" : currentUserName,
                Email = string.IsNullOrWhiteSpace(currentUserEmail) ? "learner@acadprep.com" : currentUserEmail,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            db.UserProfiles.Add(newProfile);
            try
            {
                await db.SaveChangesAsync(ct);
                users.Add(newProfile);
            }
            catch
            {
                // In case of concurrent add or non-blocking failure
            }
        }

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

        var userMap = users.ToDictionary(u => u.UserId, u => u.FullName);
        var allUserIds = users.Select(u => u.UserId)
            .Union(scores.Keys)
            .Union(streaks.Keys)
            .ToList();

        if (!string.IsNullOrEmpty(currentUserId) && currentUserId != "guest" && !allUserIds.Contains(currentUserId))
        {
            allUserIds.Add(currentUserId);
        }

        var entries = new List<LeaderboardEntryDto>();
        foreach (var uid in allUserIds)
        {
            scores.TryGetValue(uid, out var userScore);
            streaks.TryGetValue(uid, out var userStreak);

            var name = userMap.TryGetValue(uid, out var n)
                ? n
                : (uid == currentUserId ? (currentUserName ?? "Default Learner") : $"Learner {uid[..Math.Min(6, uid.Length)]}");

            entries.Add(new LeaderboardEntryDto(
                0,
                uid,
                name,
                userScore?.TotalScore ?? 0,
                userScore?.ExamsDone ?? 0,
                userStreak?.CurrentStreak ?? 0,
                uid == currentUserId
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

        // Short cache of 15s to keep real-time accuracy while reducing DB thrashing
        await cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(15), ct);
        return Result.Success(result);
    }
}
