using Identity.API.Data;

namespace Identity.API.Services;

public interface IOtpService
{
    Task<(bool Success, string Message, string? Otp)> GenerateOtpAsync(AppUser user);
    Task<(bool IsValid, string Message)> ValidateOtpAsync(AppUser user, string inputOtp);
}
