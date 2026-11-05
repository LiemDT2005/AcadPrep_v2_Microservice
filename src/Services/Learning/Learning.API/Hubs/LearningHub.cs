using Learning.API.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace Learning.API.Hubs;

public interface ILearningRealtimeNotifier
{
    Task NotifyAchievementUnlockedAsync(string userId, AchievementDto achievement, CancellationToken ct = default);
    Task NotifyStreakUpdatedAsync(string userId, int currentStreak, int maxStreak, CancellationToken ct = default);
    Task NotifyLeaderboardUpdatedAsync(CancellationToken ct = default);
}

public class LearningHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
    }

    public async Task LeaveUserGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
    }
}

public class SignalRRealtimeNotifier(IHubContext<LearningHub> hubContext) : ILearningRealtimeNotifier
{
    public async Task NotifyAchievementUnlockedAsync(string userId, AchievementDto achievement, CancellationToken ct = default)
    {
        await hubContext.Clients.Group($"user_{userId}")
            .SendAsync("AchievementUnlocked", new
            {
                userId,
                achievement.AchievementId,
                achievement.Name,
                achievement.Description,
                achievement.IconUrl,
                achievement.ConditionType,
                achievement.ConditionValue,
                achievement.UnlockedAt
            }, ct);

        await hubContext.Clients.All
            .SendAsync("AchievementUnlocked", new
            {
                userId,
                achievement.AchievementId,
                achievement.Name,
                achievement.Description,
                achievement.IconUrl,
                achievement.ConditionType,
                achievement.ConditionValue,
                achievement.UnlockedAt
            }, ct);
    }

    public async Task NotifyStreakUpdatedAsync(string userId, int currentStreak, int maxStreak, CancellationToken ct = default)
    {
        await hubContext.Clients.All
            .SendAsync("StreakUpdated", new
            {
                userId,
                currentStreak,
                maxStreak,
                updatedAt = DateTime.UtcNow
            }, ct);
    }

    public async Task NotifyLeaderboardUpdatedAsync(CancellationToken ct = default)
    {
        await hubContext.Clients.All
            .SendAsync("LeaderboardUpdated", new
            {
                updatedAt = DateTime.UtcNow
            }, ct);
    }
}
