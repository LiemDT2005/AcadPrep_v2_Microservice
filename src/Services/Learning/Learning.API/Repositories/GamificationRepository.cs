using Learning.API.Data;
using Learning.API.Entities;
using Learning.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Repositories;

public class GamificationRepository(LearningDbContext db) : IGamificationRepository
{
    public async Task<List<Achievement>> GetAllAchievementsAsync(CancellationToken ct = default)
    {
        return await db.Achievements.AsNoTracking().ToListAsync(ct);
    }

    public async Task<List<Achievement>> GetLockedAchievementsAsync(IEnumerable<int> unlockedIds, CancellationToken ct = default)
    {
        var ids = unlockedIds.ToList();
        return await db.Achievements
            .Where(a => !ids.Contains(a.Id))
            .ToListAsync(ct);
    }

    public async Task<Achievement?> GetAchievementByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Achievements.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task AddAchievementAsync(Achievement achievement, CancellationToken ct = default)
    {
        await db.Achievements.AddAsync(achievement, ct);
    }

    public void RemoveAchievement(Achievement achievement)
    {
        db.Achievements.Remove(achievement);
    }

    public async Task<Dictionary<int, DateTime>> GetUserUnlockedAchievementsAsync(string userId, CancellationToken ct = default)
    {
        return await db.UserAchievements.AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .ToDictionaryAsync(ua => ua.AchievementId, ua => ua.UnlockedAt, ct);
    }

    public async Task<List<int>> GetUserUnlockedAchievementIdsAsync(string userId, CancellationToken ct = default)
    {
        return await db.UserAchievements.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.AchievementId)
            .ToListAsync(ct);
    }

    public async Task AddUserAchievementAsync(UserAchievement userAchievement, CancellationToken ct = default)
    {
        await db.UserAchievements.AddAsync(userAchievement, ct);
    }

    public async Task<StudyStreak?> GetStudyStreakByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await db.StudyStreaks.FirstOrDefaultAsync(s => s.UserId == userId, ct);
    }

    public async Task<List<StudyStreak>> GetAllStudyStreaksAsync(CancellationToken ct = default)
    {
        return await db.StudyStreaks.AsNoTracking().ToListAsync(ct);
    }

    public async Task<List<StudyStreak>> GetExpiredStreaksAsync(DateOnly yesterday, CancellationToken ct = default)
    {
        return await db.StudyStreaks
            .Where(s => s.LastActiveDate < yesterday && s.CurrentStreak > 0)
            .ToListAsync(ct);
    }

    public async Task<int> CountActiveUsersSinceAsync(DateOnly sinceDate, CancellationToken ct = default)
    {
        return await db.StudyStreaks.CountAsync(s => s.LastActiveDate >= sinceDate, ct);
    }

    public async Task AddStudyStreakAsync(StudyStreak streak, CancellationToken ct = default)
    {
        await db.StudyStreaks.AddAsync(streak, ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await db.SaveChangesAsync(ct);
    }
}
