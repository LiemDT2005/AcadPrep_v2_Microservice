using System.Text.Json;
using Learning.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Learning.Infrastructure.Caching;

public class RedisLearningCache(IDistributedCache cache, ILogger<RedisLearningCache> logger) : ILearningCache
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await cache.GetStringAsync(key, ct);
            if (string.IsNullOrEmpty(bytes)) return default;
            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis GET failed for key {Key}. Falling back to DB.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromHours(1)
            };
            var json = JsonSerializer.Serialize(value);
            await cache.SetStringAsync(key, json, options, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis SET failed for key {Key}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis REMOVE failed for key {Key}.", key);
        }
    }

    public async Task InvalidateUserAsync(string userId, CancellationToken ct = default)
    {
        await RemoveAsync($"perf_analysis_{userId}", ct);
        await RemoveAsync($"score_progress_{userId}", ct);
        await RemoveAsync($"streak_{userId}", ct);
        await RemoveAsync("leaderboard_score", ct);
        await RemoveAsync("leaderboard_streak", ct);
    }

    public async Task InvalidateAdminReportsAsync(CancellationToken ct = default)
    {
        await RemoveAsync("admin_user_stats", ct);
        await RemoveAsync("leaderboard_score", ct);
        await RemoveAsync("leaderboard_streak", ct);
    }
}
