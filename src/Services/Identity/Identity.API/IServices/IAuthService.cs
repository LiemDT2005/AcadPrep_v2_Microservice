using Identity.API.DTOs;

namespace Identity.API.IServices;

public interface IAuthService
{
    Task<(bool Succeeded, string? Error, RegisterResponse? Response)> RegisterAsync(RegisterRequest request);
    Task<(bool Succeeded, string? Error, AuthResponse? Response)> VerifyOtpAsync(VerifyOtpRequest request, string? clientIp);
    Task<(bool Succeeded, string? Error, string? Message)> ResendOtpAsync(ResendOtpRequest request);
    Task<(bool Succeeded, string? Error, AuthResponse? Response, bool IsLockedOut)> LoginAsync(LoginRequest request, string? clientIp);
    Task<(bool Succeeded, string? Error, AuthResponse? Response)> LoginGoogleAsync(GoogleLoginRequest request, string? clientIp);
    Task<(bool Succeeded, string? Error, AuthResponse? Response)> RefreshTokenAsync(RefreshTokenRequest request, string? clientIp);
    Task<(bool Succeeded, string? Error)> LogoutAsync(string? authHeader, string? refreshToken, string? clientIp);
    Task<(bool Succeeded, string? Error, UserProfileDto? Profile)> GetProfileAsync(string userId);
    Task<(bool Succeeded, string? Error, UserProfileDto? Profile)> UpdateProfileAsync(string userId, UpdateProfileRequest request);
    Task<(bool Succeeded, string? Error)> ChangePasswordAsync(string userId, ChangePasswordRequest request);
    Task<(bool Succeeded, string? Error, string? Message)> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<(bool Succeeded, string? Error, string? Message)> ResetPasswordAsync(ResetPasswordRequest request);
}
