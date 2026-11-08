using System.Collections.Concurrent;
using Identity.API.IServices;
using StackExchange.Redis;

namespace Identity.API.Services;

public class TokenBlacklistService : ITokenBlacklistService
{
    private static readonly ConcurrentDictionary<string, DateTime> LocalBlacklist = new();
    private static readonly ConcurrentDictionary<string, DateTime> LocalUserBlacklist = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TokenBlacklistService> _logger;

    public TokenBlacklistService(
        IServiceProvider serviceProvider,
        ILogger<TokenBlacklistService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private IConnectionMultiplexer? GetRedis()
    {
        try
        {
            return _serviceProvider.GetService<IConnectionMultiplexer>();
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
                _logger.LogInformation("Access Token (jti: {Jti}) has been blacklisted in Redis for {Duration}", jti, remainingLifetime);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to blacklist token in Redis. Falling back to local memory.");
            }
        }

        // Fallback in-memory
        LocalBlacklist[jti] = DateTime.UtcNow.Add(remainingLifetime);
        _logger.LogInformation("Access Token (jti: {Jti}) blacklisted in Memory fallback until {Expiry}", jti, LocalBlacklist[jti]);
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
                _logger.LogWarning(ex, "Failed to query Redis blacklist. Falling back to local memory.");
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

    public async Task BlacklistUserAsync(string userId, TimeSpan remainingLifetime)
    {
        if (string.IsNullOrEmpty(userId) || remainingLifetime <= TimeSpan.Zero) return;

        var redis = GetRedis();
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                await db.StringSetAsync($"bl:user:{userId}", "1", remainingLifetime);
                _logger.LogInformation("User (id: {UserId}) has been blacklisted in Redis for {Duration}", userId, remainingLifetime);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to blacklist user in Redis. Falling back to local memory.");
            }
        }

        LocalUserBlacklist[userId] = DateTime.UtcNow.Add(remainingLifetime);
        _logger.LogInformation("User (id: {UserId}) blacklisted in Memory fallback until {Expiry}", userId, LocalUserBlacklist[userId]);
    }

    public async Task<bool> IsUserBlacklistedAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return false;

        var redis = GetRedis();
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                return await db.KeyExistsAsync($"bl:user:{userId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Redis user blacklist. Falling back to local memory.");
            }
        }

        if (LocalUserBlacklist.TryGetValue(userId, out var expiry))
        {
            if (DateTime.UtcNow < expiry)
            {
                return true;
            }
            LocalUserBlacklist.TryRemove(userId, out _);
        }

        return false;
    }

    public async Task UnblacklistUserAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return;

        var redis = GetRedis();
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                await db.KeyDeleteAsync($"bl:user:{userId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove user from Redis blacklist.");
            }
        }

        LocalUserBlacklist.TryRemove(userId, out _);
        _logger.LogInformation("User (id: {UserId}) removed from blacklist.", userId);
    }
}
