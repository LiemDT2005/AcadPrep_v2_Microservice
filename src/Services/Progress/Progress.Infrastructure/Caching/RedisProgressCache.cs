using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Progress.Application.Interfaces;

namespace Progress.Infrastructure.Caching;

public class RedisProgressCache(IDistributedCache cache, ILogger<RedisProgressCache> logger) : IProgressCache
{
    private static string Key(string userId) => $"progress:summary:{userId}";

    public async Task SetUserSummaryAsync(string userId, object summary, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(summary);
            await cache.SetStringAsync(Key(userId), json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis SET failed for {UserId}", userId);
        }
    }

    public async Task<T?> GetUserSummaryAsync<T>(string userId, CancellationToken ct = default)
    {
        try
        {
            var json = await cache.GetStringAsync(Key(userId), ct);
            return json is null ? default : JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis GET failed for {UserId}", userId);
            return default;
        }
    }

    public async Task InvalidateAsync(string userId, CancellationToken ct = default)
    {
        try { await cache.RemoveAsync(Key(userId), ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Redis REMOVE failed for {UserId}", userId); }
    }
}
