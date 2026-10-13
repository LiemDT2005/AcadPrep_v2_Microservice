using System.Collections.Concurrent;
using StackExchange.Redis;

namespace Identity.API.Services;

public class TokenBlacklistService(
    IServiceProvider serviceProvider,
    ILogger<TokenBlacklistService> logger) : ITokenBlacklistService
{
    private static readonly ConcurrentDictionary<string, DateTime> LocalBlacklist = new();

    private IConnectionMultiplexer? GetRedis()
    {
        try
        {
            return serviceProvider.GetService<IConnectionMultiplexer>();
        }
        catch
        {
            return null;
        }
    }

    public async Task BlacklistTokenAsync(string jti, TimeSpan remainingLifetime)
    {
        if (remainingLifetime <= TimeSpan.Zero) return;

        var redis = GetRedis();
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                await db.StringSetAsync($"bl:{jti}", "1", remainingLifetime);
                logger.LogInformation("Access Token (jti: {Jti}) has been blacklisted in Redis for {Duration}", jti, remainingLifetime);
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to blacklist token in Redis. Falling back to local memory.");
            }
        }

        // Fallback in-memory
        LocalBlacklist[jti] = DateTime.UtcNow.Add(remainingLifetime);
        logger.LogInformation("Access Token (jti: {Jti}) blacklisted in Memory fallback until {Expiry}", jti, LocalBlacklist[jti]);
    }

    public async Task<bool> IsBlacklistedAsync(string jti)
    {
        var redis = GetRedis();
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                return await db.KeyExistsAsync($"bl:{jti}");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to query Redis blacklist. Falling back to local memory.");
            }
        }

        // Fallback in-memory check
        if (LocalBlacklist.TryGetValue(jti, out var expiry))
        {
            if (DateTime.UtcNow < expiry)
            {
                return true;
            }
            // Expired, cleanup
            LocalBlacklist.TryRemove(jti, out _);
        }

        return false;
    }
}
