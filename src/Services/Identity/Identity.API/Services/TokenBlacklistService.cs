using System.Collections.Concurrent;
using Identity.API.IServices;
using StackExchange.Redis;

namespace Identity.API.Services;

public class TokenBlacklistService : ITokenBlacklistService
{
    private static readonly ConcurrentDictionary<string, DateTime> LocalBlacklist = new();

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
}
