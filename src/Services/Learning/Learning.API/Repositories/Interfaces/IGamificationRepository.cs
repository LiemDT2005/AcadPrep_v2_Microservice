using Learning.API.Entities;

namespace Learning.API.Repositories.Interfaces;

public interface IGamificationRepository
{
    Task<List<Achievement>> GetAllAchievementsAsync(CancellationToken ct = default);
    Task<List<Achievement>> GetLockedAchievementsAsync(IEnumerable<int> unlockedIds, CancellationToken ct = default);
    Task<Achievement?> GetAchievementByIdAsync(int id, CancellationToken ct = default);
    Task AddAchievementAsync(Achievement achievement, CancellationToken ct = default);
    void RemoveAchievement(Achievement achievement);

    Task<Dictionary<int, DateTime>> GetUserUnlockedAchievementsAsync(string userId, CancellationToken ct = default);
    Task<List<int>> GetUserUnlockedAchievementIdsAsync(string userId, CancellationToken ct = default);
    Task AddUserAchievementAsync(UserAchievement userAchievement, CancellationToken ct = default);

    Task<StudyStreak?> GetStudyStreakByUserIdAsync(string userId, CancellationToken ct = default);
    Task<List<StudyStreak>> GetAllStudyStreaksAsync(CancellationToken ct = default);
    Task<List<StudyStreak>> GetExpiredStreaksAsync(DateOnly yesterday, CancellationToken ct = default);
    Task<int> CountActiveUsersSinceAsync(DateOnly sinceDate, CancellationToken ct = default);
    Task AddStudyStreakAsync(StudyStreak streak, CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
