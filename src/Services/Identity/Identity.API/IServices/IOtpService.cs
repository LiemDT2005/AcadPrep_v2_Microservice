using Identity.API.Models;

namespace Identity.API.IServices;

public interface IOtpService
{
    Task<(bool Success, string Message, string? Otp)> GenerateOtpAsync(AppUser user);
    Task<(bool IsValid, string Message)> ValidateOtpAsync(AppUser user, string inputOtp);
}
