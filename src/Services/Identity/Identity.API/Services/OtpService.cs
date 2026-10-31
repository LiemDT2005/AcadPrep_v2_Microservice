using System.Collections.Concurrent;
using System.Security.Cryptography;
using Identity.API.IServices;
using Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

namespace Identity.API.Services;

public class OtpService : IOtpService
{
    private const int OtpExpiryMinutes = 5;
    private const int ResendCooldownSeconds = 60;
    private const int MaxFailedAttempts = 5;

    private readonly UserManager<AppUser> _userManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        UserManager<AppUser> userManager,
        IServiceProvider serviceProvider,
        ILogger<OtpService> logger)
    {
        _userManager = userManager;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private static readonly ConcurrentDictionary<string, int> LocalFailedAttempts = new();
    private static readonly ConcurrentDictionary<string, DateTime> LocalCooldowns = new();

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

    public async Task<(bool Success, string Message, string? Otp)> GenerateOtpAsync(AppUser user)
    {
        var email = user.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var redis = GetRedis();

        // 1. Check Resend Cooldown (60 seconds)
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                if (await db.KeyExistsAsync($"otp_cd:{email}"))
                {
                    var ttl = await db.KeyTimeToLiveAsync($"otp_cd:{email}");
                    return (false, $"Please wait {ttl?.Seconds ?? ResendCooldownSeconds} seconds before requesting a new code.", null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis error while checking OTP cooldown");
            }
        }
        else if (LocalCooldowns.TryGetValue(email, out var cdEnd) && DateTime.UtcNow < cdEnd)
        {
            var remaining = (int)(cdEnd - DateTime.UtcNow).TotalSeconds;
            return (false, $"Please wait {remaining} seconds before requesting a new code.", null);
        }

        // 2. Generate secure 6-digit OTP
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiry = Common.TimeHelper.Now.AddMinutes(OtpExpiryMinutes);

        // 3. Save to User DB
        user.OtpCode = otp;
        user.OtpExpiresAt = expiry;
        user.UpdatedAt = Common.TimeHelper.Now;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return (false, "Unable to save OTP code to the system.", null);
        }

        // 4. Save to Redis / Memory Cache
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                await db.StringSetAsync($"otp:{email}", otp, TimeSpan.FromMinutes(OtpExpiryMinutes));
                await db.StringSetAsync($"otp_cd:{email}", "1", TimeSpan.FromSeconds(ResendCooldownSeconds));
                await db.KeyDeleteAsync($"otp_fail:{email}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis error while saving OTP");
            }
        }

        LocalCooldowns[email] = DateTime.UtcNow.AddSeconds(ResendCooldownSeconds);
        LocalFailedAttempts.TryRemove(email, out _);

        _logger.LogInformation("Generated OTP for {Email}: {Otp} (Expires in {Minutes}m)", email, otp, OtpExpiryMinutes);
        return (true, "OTP code generated successfully.", otp);
    }

    public async Task<(bool IsValid, string Message)> ValidateOtpAsync(AppUser user, string inputOtp)
    {
        var email = user.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var redis = GetRedis();

        // 1. Rate Limiting Brute-Force
        int currentFails = 0;
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                var failVal = await db.StringGetAsync($"otp_fail:{email}");
                if (failVal.HasValue) int.TryParse(failVal, out currentFails);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis error while reading OTP failed attempts");
            }
        }
        else
        {
            LocalFailedAttempts.TryGetValue(email, out currentFails);
        }

        if (currentFails >= MaxFailedAttempts)
        {
            return (false, "You have entered an incorrect OTP more than 5 times. Please request a new OTP code.");
        }

        // 2. Validate OTP
        string? validOtp = null;
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                validOtp = await db.StringGetAsync($"otp:{email}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis error while fetching OTP");
            }
        }

        if (string.IsNullOrEmpty(validOtp))
        {
            if (user.OtpExpiresAt == null || Common.TimeHelper.Now > user.OtpExpiresAt)
            {
                return (false, "OTP code has expired. Please request a new OTP code.");
            }
            validOtp = user.OtpCode;
        }

        if (string.IsNullOrEmpty(validOtp) || validOtp != inputOtp.Trim())
        {
            currentFails++;
            if (redis is not null && redis.IsConnected)
            {
                try
                {
                    var db = redis.GetDatabase();
                    await db.StringSetAsync($"otp_fail:{email}", currentFails, TimeSpan.FromMinutes(OtpExpiryMinutes));
                }
                catch { }
            }
            LocalFailedAttempts[email] = currentFails;

            var remainingTries = MaxFailedAttempts - currentFails;
            if (remainingTries <= 0)
            {
                user.OtpCode = null;
                user.OtpExpiresAt = null;
                await _userManager.UpdateAsync(user);

                if (redis is not null && redis.IsConnected)
                {
                    try { await redis.GetDatabase().KeyDeleteAsync($"otp:{email}"); } catch { }
                }

                return (false, "You have entered an incorrect OTP 5 times. This OTP has been invalidated for security.");
            }

            return (false, $"Incorrect OTP code. You have {remainingTries} attempts remaining.");
        }

        // 3. Valid OTP
        user.OtpCode = null;
        user.OtpExpiresAt = null;
        await _userManager.UpdateAsync(user);

        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                await db.KeyDeleteAsync($"otp:{email}");
                await db.KeyDeleteAsync($"otp_fail:{email}");
            }
            catch { }
        }

        LocalFailedAttempts.TryRemove(email, out _);

        return (true, "OTP code verified successfully.");
    }
}
