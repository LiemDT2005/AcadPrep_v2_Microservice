using System.Collections.Concurrent;
using System.Security.Cryptography;
using Identity.API.Data;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

namespace Identity.API.Services;

public class OtpService(
    UserManager<AppUser> userManager,
    IServiceProvider serviceProvider,
    ILogger<OtpService> logger) : IOtpService
{
    private const int OtpExpiryMinutes = 5;
    private const int ResendCooldownSeconds = 60;
    private const int MaxFailedAttempts = 5;

    // Bộ nhớ in-memory theo dõi số lần thử sai và cooldown khi Redis chưa chạy
    private static readonly ConcurrentDictionary<string, int> LocalFailedAttempts = new();
    private static readonly ConcurrentDictionary<string, DateTime> LocalCooldowns = new();

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

    public async Task<(bool Success, string Message, string? Otp)> GenerateOtpAsync(AppUser user)
    {
        var email = user.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var redis = GetRedis();

        // 1. Kiểm tra Cooldown chống spam gửi mã (60 giây)
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                if (await db.KeyExistsAsync($"otp_cd:{email}"))
                {
                    var ttl = await db.KeyTimeToLiveAsync($"otp_cd:{email}");
                    return (false, $"Vui lòng đợi {ttl?.Seconds ?? ResendCooldownSeconds} giây trước khi yêu cầu mã mới.", null);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis error while checking OTP cooldown");
            }
        }
        else if (LocalCooldowns.TryGetValue(email, out var cdEnd) && DateTime.UtcNow < cdEnd)
        {
            var remaining = (int)(cdEnd - DateTime.UtcNow).TotalSeconds;
            return (false, $"Vui lòng đợi {remaining} giây trước khi yêu cầu mã mới.", null);
        }

        // 2. Sinh mã 6 số ngẫu nhiên an toàn (Cryptographically secure)
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var expiry = Common.TimeHelper.Now.AddMinutes(OtpExpiryMinutes);

        // 3. Lưu vào User DB
        user.OtpCode = otp;
        user.OtpExpiresAt = expiry;
        user.UpdatedAt = Common.TimeHelper.Now;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return (false, "Không thể lưu mã OTP vào hệ thống.", null);
        }

        // 4. Lưu vào Redis / Memory Cache
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                await db.StringSetAsync($"otp:{email}", otp, TimeSpan.FromMinutes(OtpExpiryMinutes));
                await db.StringSetAsync($"otp_cd:{email}", "1", TimeSpan.FromSeconds(ResendCooldownSeconds));
                await db.KeyDeleteAsync($"otp_fail:{email}"); // reset failed attempts
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis error while saving OTP");
            }
        }

        LocalCooldowns[email] = DateTime.UtcNow.AddSeconds(ResendCooldownSeconds);
        LocalFailedAttempts.TryRemove(email, out _);

        logger.LogInformation("Generated OTP for {Email}: {Otp} (Expires in {Minutes}m)", email, otp, OtpExpiryMinutes);
        return (true, "Mã OTP đã được tạo thành công.", otp);
    }

    public async Task<(bool IsValid, string Message)> ValidateOtpAsync(AppUser user, string inputOtp)
    {
        var email = user.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var redis = GetRedis();

        // 1. Kiểm tra số lần thử sai (Brute-force protection)
        int currentFails = 0;
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                var val = await db.StringGetAsync($"otp_fail:{email}");
                if (val.HasValue) int.TryParse(val, out currentFails);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis error checking failed attempts");
            }
        }
        else
        {
            LocalFailedAttempts.TryGetValue(email, out currentFails);
        }

        if (currentFails >= MaxFailedAttempts)
        {
            // Hủy mã OTP luôn
            user.OtpCode = null;
            user.OtpExpiresAt = null;
            await userManager.UpdateAsync(user);

            if (redis is not null && redis.IsConnected)
            {
                var db = redis.GetDatabase();
                await db.KeyDeleteAsync($"otp:{email}");
                await db.KeyDeleteAsync($"otp_fail:{email}");
            }
            LocalFailedAttempts.TryRemove(email, out _);

            return (false, "Bạn đã nhập sai quá 5 lần. Mã OTP đã bị hủy vì lý do bảo mật. Vui lòng yêu cầu gửi lại mã mới.");
        }

        // 2. Kiểm tra hạn OTP
        if (string.IsNullOrEmpty(user.OtpCode) || !user.OtpExpiresAt.HasValue || user.OtpExpiresAt.Value < Common.TimeHelper.Now)
        {
            return (false, "Mã OTP đã hết hạn hoặc không tồn tại.");
        }

        // 3. So khớp mã OTP
        if (user.OtpCode != inputOtp.Trim())
        {
            currentFails++;
            if (redis is not null && redis.IsConnected)
            {
                try
                {
                    var db = redis.GetDatabase();
                    await db.StringSetAsync($"otp_fail:{email}", currentFails, TimeSpan.FromMinutes(OtpExpiryMinutes));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Redis error recording failed attempt");
                }
            }
            LocalFailedAttempts[email] = currentFails;

            var remainingTries = MaxFailedAttempts - currentFails;
            return (false, $"Mã OTP không chính xác. Bạn còn {remainingTries} lần thử.");
        }

        // 4. Đúng mã -> Xóa OTP ngay lập tức
        user.OtpCode = null;
        user.OtpExpiresAt = null;
        await userManager.UpdateAsync(user);

        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                await db.KeyDeleteAsync($"otp:{email}");
                await db.KeyDeleteAsync($"otp_fail:{email}");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Redis cleanup error");
            }
        }
        LocalFailedAttempts.TryRemove(email, out _);

        return (true, "Xác thực OTP thành công.");
    }
}
