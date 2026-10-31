using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Google.Apis.Auth;
using Identity.API.DTOs;
using Identity.API.IRepositories;
using Identity.API.IServices;
using Identity.API.Mappers;
using Identity.API.Models;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly ITokenBlacklistService _blacklistService;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IOtpService otpService,
        IEmailService emailService,
        ITokenBlacklistService blacklistService,
        SignInManager<AppUser> signInManager,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _otpService = otpService;
        _emailService = emailService;
        _blacklistService = blacklistService;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool Succeeded, string? Error, RegisterResponse? Response)> RegisterAsync(RegisterRequest request)
    {
        if (!string.IsNullOrEmpty(request.ConfirmPassword) && request.Password != request.ConfirmPassword)
            return (false, "Passwords do not match.", null);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existing = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (existing is not null)
            return (false, "This email is already registered.", null);

        var user = new AppUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            Status = UserStatus.PendingVerification,
            EmailConfirmed = false,
            AuthProvider = "Local",
            CreatedAt = Common.TimeHelper.Now
        };

        var result = await _userRepository.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)), null);

        await _userRepository.AddToRoleAsync(user, "Learner");

        var (otpSuccess, _, otp) = await _otpService.GenerateOtpAsync(user);
        if (otpSuccess && !string.IsNullOrEmpty(otp))
        {
            await _emailService.SendOtpEmailAsync(user.Email!, otp, user.FullName);
        }

        return (true, null, new RegisterResponse(
            Message: "Registration successful! Please enter the 6-digit OTP code sent to your email to activate your account.",
            Email: user.Email!,
            UserId: user.Id,
            Status: user.Status.ToString()
        ));
    }

    public async Task<(bool Succeeded, string? Error, AuthResponse? Response)> VerifyOtpAsync(VerifyOtpRequest request, string? clientIp)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user is null)
            return (false, "Account with this email was not found.", null);

        if (user.EmailConfirmed && user.Status == UserStatus.Active)
            return (false, "This account has already been activated.", null);

        var (isValid, message) = await _otpService.ValidateOtpAsync(user, request.Otp);
        if (!isValid)
            return (false, message, null);

        user.EmailConfirmed = true;
        user.Status = UserStatus.Active;
        user.UpdatedAt = Common.TimeHelper.Now;
        user.LastLoginAt = Common.TimeHelper.Now;
        await _userRepository.UpdateAsync(user);

        var roles = await _userRepository.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.CreateToken(user, roles);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, clientIp);

        return (true, null, UserMapper.ToAuthResponse(
            user, token, expiresAt, roles, refreshToken.Token, refreshToken.ExpiresAt
        ));
    }

    public async Task<(bool Succeeded, string? Error, string? Message)> ResendOtpAsync(ResendOtpRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user is null)
            return (false, "Account with this email was not found.", null);

        if (user.EmailConfirmed && user.Status == UserStatus.Active)
            return (false, "This account is already active. You can log in directly.", null);

        var (success, message, otp) = await _otpService.GenerateOtpAsync(user);
        if (!success)
            return (false, message, null);

        if (!string.IsNullOrEmpty(otp))
        {
            await _emailService.SendOtpEmailAsync(user.Email!, otp, user.FullName);
        }

        return (true, null, "A new verification code has been sent to your email.");
    }

    public async Task<(bool Succeeded, string? Error, AuthResponse? Response, bool IsLockedOut)> LoginAsync(LoginRequest request, string? clientIp)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user is null)
            return (false, "Invalid email or password.", null, false);

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            var lockoutEnd = user.LockoutEnd?.ToOffset(TimeSpan.FromHours(7)).ToString("MM/dd/yyyy HH:mm:ss") ?? "5 minutes";
            return (false, $"Your account is temporarily locked due to multiple failed login attempts. Please try again after {lockoutEnd}.", null, true);
        }

        if (!signInResult.Succeeded)
        {
            var remaining = 5 - user.AccessFailedCount;
            return (false, remaining > 0
                ? $"Invalid email or password. You have {remaining} attempts remaining before account lockout."
                : "Invalid email or password.", null, false);
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            return (false, "Account not activated. Please verify the OTP code sent to your email before signing in.", null, false);
        }

        if (user.Status == UserStatus.Banned)
        {
            return (false, "Your account has been suspended by an administrator.", null, false);
        }

        user.LastLoginAt = Common.TimeHelper.Now;
        await _userRepository.UpdateAsync(user);

        var roles = await _userRepository.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.CreateToken(user, roles);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, clientIp);

        return (true, null, UserMapper.ToAuthResponse(
            user, token, expiresAt, roles, refreshToken.Token, refreshToken.ExpiresAt
        ), false);
    }

    public async Task<(bool Succeeded, string? Error, AuthResponse? Response)> LoginGoogleAsync(GoogleLoginRequest request, string? clientIp)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (!string.IsNullOrEmpty(clientId))
            {
                settings.Audience = [clientId];
            }
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Google token validation failed");
            return (false, "Google ID Token is invalid or expired.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google token");
            return (false, "Unable to authenticate with Google. Please try again.", null);
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);

        if (user is null)
        {
            user = new AppUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                FullName = !string.IsNullOrWhiteSpace(payload.Name) ? payload.Name : normalizedEmail.Split('@')[0],
                Avatar = payload.Picture,
                Status = UserStatus.Active,
                EmailConfirmed = true,
                AuthProvider = "Google",
                CreatedAt = Common.TimeHelper.Now,
                LastLoginAt = Common.TimeHelper.Now
            };

            var createResult = await _userRepository.CreateAsync(user, Guid.NewGuid().ToString("N") + "Aa1@!");
            if (!createResult.Succeeded)
            {
                return (false, "Unable to create account from Google data: " + string.Join("; ", createResult.Errors.Select(e => e.Description)), null);
            }

            await _userRepository.AddToRoleAsync(user, "Learner");
            _logger.LogInformation("New user registered via Google: {Email}", user.Email);
        }
        else
        {
            if (user.Status == UserStatus.Banned)
            {
                return (false, "Your account has been suspended by an administrator.", null);
            }

            if (string.IsNullOrEmpty(user.Avatar) && !string.IsNullOrEmpty(payload.Picture))
            {
                user.Avatar = payload.Picture;
            }

            if (!user.EmailConfirmed || user.Status == UserStatus.PendingVerification)
            {
                user.EmailConfirmed = true;
                user.Status = UserStatus.Active;
            }

            user.LastLoginAt = Common.TimeHelper.Now;
            await _userRepository.UpdateAsync(user);
        }

        var roles = await _userRepository.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.CreateToken(user, roles);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, clientIp);

        return (true, null, UserMapper.ToAuthResponse(
            user, token, expiresAt, roles, refreshToken.Token, refreshToken.ExpiresAt
        ));
    }

    public async Task<(bool Succeeded, string? Error, AuthResponse? Response)> RefreshTokenAsync(RefreshTokenRequest request, string? clientIp)
    {
        var existingToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        if (existingToken is null)
        {
            return (false, "Refresh token not found.", null);
        }

        if (existingToken.IsRevoked)
        {
            _logger.LogWarning("Revoked Refresh Token used! Possible replay attack for UserId: {UserId}", existingToken.UserId);
            var activeTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(existingToken.UserId);
            foreach (var t in activeTokens)
            {
                t.RevokedAt = Common.TimeHelper.Now;
                t.ReasonRevoked = "Security alert: Attempted reuse of revoked token";
            }
            await _refreshTokenRepository.SaveChangesAsync();

            return (false, "Session terminated due to security policy. Please log in again.", null);
        }

        if (existingToken.IsExpired)
        {
            return (false, "Session has expired. Please log in again.", null);
        }

        var user = existingToken.User ?? await _userRepository.GetByIdAsync(existingToken.UserId);
        if (user is null || user.Status == UserStatus.Banned)
        {
            return (false, "Account is invalid or disabled.", null);
        }

        var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, clientIp);

        existingToken.RevokedAt = Common.TimeHelper.Now;
        existingToken.RevokedByIp = clientIp;
        existingToken.ReplacedByToken = newRefreshToken.Token;
        existingToken.ReasonRevoked = "Replaced by new token during refresh";
        await _refreshTokenRepository.UpdateAsync(existingToken);
        await _refreshTokenRepository.SaveChangesAsync();

        var roles = await _userRepository.GetRolesAsync(user);
        var (jwtToken, expiresAt) = _tokenService.CreateToken(user, roles);

        return (true, null, UserMapper.ToAuthResponse(
            user, jwtToken, expiresAt, roles, newRefreshToken.Token, newRefreshToken.ExpiresAt
        ));
    }

    public async Task<(bool Succeeded, string? Error)> LogoutAsync(string? authHeader, string? refreshToken, string? clientIp)
    {
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var rawJwt = authHeader.Substring("Bearer ".Length).Trim();
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(rawJwt))
                {
                    var jwt = handler.ReadJwtToken(rawJwt);
                    var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                    var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;

                    if (!string.IsNullOrEmpty(jti) && long.TryParse(expClaim, out var expUnix))
                    {
                        var expUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                        var remainingLifetime = expUtc - DateTime.UtcNow;
                        if (remainingLifetime > TimeSpan.Zero)
                        {
                            await _blacklistService.BlacklistTokenAsync(jti, remainingLifetime);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract JTI from JWT during logout");
            }
        }

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var tokenEntity = await _refreshTokenRepository.GetByTokenAsync(refreshToken);
            if (tokenEntity is not null && !tokenEntity.IsRevoked)
            {
                tokenEntity.RevokedAt = Common.TimeHelper.Now;
                tokenEntity.RevokedByIp = clientIp;
                tokenEntity.ReasonRevoked = "User explicitly logged out";
                await _refreshTokenRepository.UpdateAsync(tokenEntity);
                await _refreshTokenRepository.SaveChangesAsync();
            }
        }

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error, UserProfileDto? Profile)> GetProfileAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            return (false, "Account not found.", null);

        var roles = await _userRepository.GetRolesAsync(user);
        return (true, null, UserMapper.ToUserProfileDto(user, roles));
    }

    public async Task<(bool Succeeded, string? Error, UserProfileDto? Profile)> UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            return (false, "Account not found.", null);

        user.FullName = request.FullName.Trim();
        user.Avatar = string.IsNullOrWhiteSpace(request.Avatar) ? null : request.Avatar.Trim();
        user.UpdatedAt = Common.TimeHelper.Now;

        var result = await _userRepository.UpdateAsync(user);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)), null);

        var roles = await _userRepository.GetRolesAsync(user);
        return (true, null, UserMapper.ToUserProfileDto(user, roles));
    }

    public async Task<(bool Succeeded, string? Error)> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            return (false, "User account not found.");

        if (user.AuthProvider == "Google" && string.IsNullOrEmpty(user.PasswordHash))
            return (false, "Google-authenticated accounts cannot change password here.");

        if (request.NewPassword != request.ConfirmPassword)
            return (false, "Passwords do not match.");

        var result = await _userRepository.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return (false, errors.Contains("Incorrect password", StringComparison.OrdinalIgnoreCase) ||
                           errors.Contains("PasswordMismatch", StringComparison.OrdinalIgnoreCase)
                ? "Current password is incorrect."
                : errors);
        }

        user.UpdatedAt = Common.TimeHelper.Now;
        await _userRepository.UpdateAsync(user);
        await _userRepository.UpdateSecurityStampAsync(user);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error, string? Message)> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);

        if (user is not null && user.Status != UserStatus.Banned)
        {
            var (success, msg, otp) = await _otpService.GenerateOtpAsync(user);
            if (success && !string.IsNullOrEmpty(otp))
            {
                await _emailService.SendOtpEmailAsync(user.Email!, otp, user.FullName);
            }
            else
            {
                return (false, msg, null);
            }
        }

        return (true, null, "If your email is registered in our system, a password recovery code has been dispatched.");
    }

    public async Task<(bool Succeeded, string? Error, string? Message)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user is null)
            return (false, "Invalid password reset request.", null);

        var (isValid, msg) = await _otpService.ValidateOtpAsync(user, request.Otp);
        if (!isValid)
            return (false, msg, null);

        var resetToken = await _userRepository.GeneratePasswordResetTokenAsync(user);
        var result = await _userRepository.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
            return (false, string.Join("; ", result.Errors.Select(e => e.Description)), null);

        user.UpdatedAt = Common.TimeHelper.Now;
        await _userRepository.UpdateAsync(user);
        await _userRepository.UpdateSecurityStampAsync(user);

        return (true, null, "Password reset successfully. You can now log in with your new password.");
    }
}
