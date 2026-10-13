using System.ComponentModel.DataAnnotations;

namespace Identity.API.Models;

public record RegisterRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    string Password,

    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    string FullName,

    string? ConfirmPassword = null);

public record LoginRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,

    [Required(ErrorMessage = "Password is required.")]
    string Password);

public record UserProfileDto(
    string Id,
    string Email,
    string FullName,
    string? Avatar,
    string Status,
    List<string> Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record AuthResponse(
    string Token,
    string Email,
    string FullName,
    string UserId,
    DateTime ExpiresAt,
    List<string> Roles,
    string? Avatar = null,
    string? Status = null,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAt = null);

public record VerifyOtpRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email,

    [Required(ErrorMessage = "OTP code is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits.")]
    string Otp);

public record ResendOtpRequest(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    string Email);

public record GoogleLoginRequest(
    [Required(ErrorMessage = "Google ID token is required.")]
    string IdToken);

public record RefreshTokenRequest(
    [Required(ErrorMessage = "Refresh token is required.")]
    string RefreshToken);

public record LogoutRequest(
    string? RefreshToken = null);

public record RegisterResponse(
    string Message,
    string Email,
    string UserId,
    string Status);

