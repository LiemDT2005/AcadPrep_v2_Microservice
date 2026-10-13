using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Google.Apis.Auth;
using Identity.API.Data;
using Identity.API.Models;
using Identity.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    TokenService tokenService,
    IOtpService otpService,
    IEmailService emailService,
    ITokenBlacklistService blacklistService,
    IdentityDbContext dbContext,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    private string? GetClientIp() =>
        HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var ip)
            ? ip.FirstOrDefault()
            : HttpContext.Connection.RemoteIpAddress?.ToString();

    // ==========================================
    // UC-1: Đăng ký tài khoản (Register)
    // ==========================================
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!string.IsNullOrEmpty(request.ConfirmPassword) && request.Password != request.ConfirmPassword)
            return BadRequest(new { error = "Mật khẩu xác nhận không khớp." });

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existing = await userManager.FindByEmailAsync(normalizedEmail);
        if (existing is not null)
            return BadRequest(new { error = "Email này đã được sử dụng." });

        var user = new AppUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            Status = UserStatus.PendingVerification, // Cần xác thực OTP
            EmailConfirmed = false,
            AuthProvider = "Local",
            CreatedAt = Common.TimeHelper.Now
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });

        // Gán Role mặc định: Learner
        await userManager.AddToRoleAsync(user, "Learner");

        // Sinh OTP và gửi email
        var (otpSuccess, otpMsg, otp) = await otpService.GenerateOtpAsync(user);
        if (otpSuccess && !string.IsNullOrEmpty(otp))
        {
            await emailService.SendOtpEmailAsync(user.Email!, otp, user.FullName);
        }

        return Ok(new RegisterResponse(
            Message: "Đăng ký thành công! Vui lòng nhập mã OTP 6 số đã được gửi tới email để kích hoạt tài khoản.",
            Email: user.Email!,
            UserId: user.Id,
            Status: user.Status.ToString()
        ));
    }

    // ==========================================
    // UC-2: Xác thực OTP kích hoạt tài khoản (Verify OTP)
    // ==========================================
    [HttpPost("verify-otp")]
    public async Task<ActionResult<AuthResponse>> VerifyOtp(VerifyOtpRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
            return NotFound(new { error = "Không tìm thấy tài khoản với email này." });

        if (user.EmailConfirmed && user.Status == UserStatus.Active)
            return BadRequest(new { error = "Tài khoản này đã được kích hoạt trước đó." });

        var (isValid, message) = await otpService.ValidateOtpAsync(user, request.Otp);
        if (!isValid)
            return BadRequest(new { error = message });

        // Kích hoạt tài khoản
        user.EmailConfirmed = true;
        user.Status = UserStatus.Active;
        user.UpdatedAt = Common.TimeHelper.Now;
        user.LastLoginAt = Common.TimeHelper.Now;
        await userManager.UpdateAsync(user);

        // Cấp phát cặp Access Token & Refresh Token
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpires) = tokenService.CreateToken(user, roles);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(user.Id, GetClientIp());

        return Ok(new AuthResponse(
            Token: accessToken,
            Email: user.Email!,
            FullName: user.FullName,
            UserId: user.Id,
            ExpiresAt: accessExpires,
            Roles: [.. roles],
            Avatar: user.Avatar,
            Status: user.Status.ToString(),
            RefreshToken: refreshToken.Token,
            RefreshTokenExpiresAt: refreshToken.ExpiresAt
        ));
    }

    // ==========================================
    // UC-2: Gửi lại mã OTP (Resend OTP)
    // ==========================================
    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(ResendOtpRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
            return Ok(new { message = "Nếu email hợp lệ, mã OTP mới sẽ được gửi về hòm thư của bạn." });

        if (user.EmailConfirmed && user.Status == UserStatus.Active)
            return BadRequest(new { error = "Tài khoản đã được kích hoạt, không cần gửi lại OTP." });

        var (success, msg, otp) = await otpService.GenerateOtpAsync(user);
        if (!success)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = msg });

        await emailService.SendOtpEmailAsync(user.Email!, otp!, user.FullName);

        return Ok(new { message = "Mã OTP mới đã được gửi thành công về email của bạn." });
    }

    // ==========================================
    // UC-5.1: Đăng nhập bằng Email/Password (Login)
    // ==========================================
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
            return Unauthorized(new { error = "Email hoặc mật khẩu không chính xác." });

        // Kiểm tra lockout chống brute-force
        if (await userManager.IsLockedOutAsync(user))
        {
            return StatusCode(StatusCodes.Status423Locked, new
            {
                error = "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 5 phút."
            });
        }

        // Kiểm tra trạng thái tài khoản
        if (user.Status == UserStatus.Banned)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên."
            });
        }

        if (user.Status == UserStatus.PendingVerification || !user.EmailConfirmed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "Tài khoản chưa được kích hoạt. Vui lòng xác thực OTP qua email.",
                requiresOtpVerification = true,
                email = user.Email
            });
        }

        // Kiểm tra mật khẩu (ghi nhận thất bại để lockout)
        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            if (check.IsLockedOut)
            {
                return StatusCode(StatusCodes.Status423Locked, new
                {
                    error = "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau 5 phút."
                });
            }

            return Unauthorized(new { error = "Email hoặc mật khẩu không chính xác." });
        }

        // Cập nhật LastLoginAt
        user.LastLoginAt = Common.TimeHelper.Now;
        await userManager.UpdateAsync(user);

        // Cấp phát Access Token + Refresh Token
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpires) = tokenService.CreateToken(user, roles);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(user.Id, GetClientIp());

        return Ok(new AuthResponse(
            Token: accessToken,
            Email: user.Email!,
            FullName: user.FullName,
            UserId: user.Id,
            ExpiresAt: accessExpires,
            Roles: [.. roles],
            Avatar: user.Avatar,
            Status: user.Status.ToString(),
            RefreshToken: refreshToken.Token,
            RefreshTokenExpiresAt: refreshToken.ExpiresAt
        ));
    }

    // ==========================================
    // UC-5.2: Đăng nhập bằng Google (OAuth2 / ID Token)
    // ==========================================
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin(GoogleLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var googleClientId = configuration["Authentication:Google:ClientId"];
            var settings = new GoogleJsonWebSignature.ValidationSettings();

            // Nếu ClientId được cung cấp thực tế, validate Audience để chống token substitution
            if (!string.IsNullOrEmpty(googleClientId) && !googleClientId.StartsWith("mock-"))
            {
                settings.Audience = [googleClientId];
            }

            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Google ID token validation failed");
            return Unauthorized(new { error = "Google token không hợp lệ hoặc đã hết hạn." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error verifying Google ID token");
            return Unauthorized(new { error = "Không thể xác thực danh tính với Google." });
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
        {
            // Case 1: Tài khoản mới -> Tự động kích hoạt
            user = new AppUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                FullName = payload.Name ?? "Google User",
                Avatar = payload.Picture,
                EmailConfirmed = payload.EmailVerified,
                Status = UserStatus.Active,
                AuthProvider = "Google",
                CreatedAt = Common.TimeHelper.Now,
                LastLoginAt = Common.TimeHelper.Now
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return BadRequest(new { error = string.Join("; ", createResult.Errors.Select(e => e.Description)) });
            }

            await userManager.AddToRoleAsync(user, "Learner");
            await userManager.AddLoginAsync(user, new UserLoginInfo("Google", payload.Subject, "Google"));
        }
        else
        {
            // Case 2: Tài khoản đã có trong hệ thống
            if (user.Status == UserStatus.Banned)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên."
                });
            }

            // Kích hoạt nếu trước đó là Pending
            if (!user.EmailConfirmed && payload.EmailVerified)
            {
                user.EmailConfirmed = true;
                user.Status = UserStatus.Active;
            }

            if (string.IsNullOrEmpty(user.Avatar) && !string.IsNullOrEmpty(payload.Picture))
            {
                user.Avatar = payload.Picture;
            }

            user.LastLoginAt = Common.TimeHelper.Now;
            user.UpdatedAt = Common.TimeHelper.Now;
            await userManager.UpdateAsync(user);

            // Gắn thông tin Google Provider nếu chưa có
            var logins = await userManager.GetLoginsAsync(user);
            if (logins.All(l => l.LoginProvider != "Google"))
            {
                await userManager.AddLoginAsync(user, new UserLoginInfo("Google", payload.Subject, "Google"));
            }
        }

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpires) = tokenService.CreateToken(user, roles);
        var refreshToken = await tokenService.GenerateRefreshTokenAsync(user.Id, GetClientIp());

        return Ok(new AuthResponse(
            Token: accessToken,
            Email: user.Email!,
            FullName: user.FullName,
            UserId: user.Id,
            ExpiresAt: accessExpires,
            Roles: [.. roles],
            Avatar: user.Avatar,
            Status: user.Status.ToString(),
            RefreshToken: refreshToken.Token,
            RefreshTokenExpiresAt: refreshToken.ExpiresAt
        ));
    }

    // ==========================================
    // UC-6: Refresh Token Rotation
    // ==========================================
    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponse>> RefreshToken(RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tokenRecord = await dbContext.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken);

        if (tokenRecord is null)
            return Unauthorized(new { error = "Refresh token không tồn tại hoặc không hợp lệ." });

        // Kiểm tra trường hợp Refresh Token Reuse (tấn công replay attack)
        if (tokenRecord.IsRevoked)
        {
            logger.LogWarning("Potential Token Reuse detected for UserId: {UserId}", tokenRecord.UserId);
            // Thu hồi toàn bộ refresh tokens còn lại của user để bảo vệ
            var activeTokens = await dbContext.RefreshTokens
                .Where(r => r.UserId == tokenRecord.UserId && r.RevokedAt == null)
                .ToListAsync();

            foreach (var t in activeTokens)
            {
                t.RevokedAt = Common.TimeHelper.Now;
                t.ReasonRevoked = "Revoked due to token reuse attack detection";
            }
            await dbContext.SaveChangesAsync();

            return Unauthorized(new { error = "Refresh token đã bị thu hồi. Vui lòng đăng nhập lại." });
        }

        if (tokenRecord.IsExpired)
            return Unauthorized(new { error = "Refresh token đã hết hạn. Vui lòng đăng nhập lại." });

        var user = tokenRecord.User;
        if (user is null || user.Status == UserStatus.Banned)
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Tài khoản không hợp lệ hoặc đã bị khóa." });

        // Thu hồi token cũ và sinh token mới (Token Rotation)
        var clientIp = GetClientIp();
        tokenRecord.RevokedAt = Common.TimeHelper.Now;
        tokenRecord.RevokedByIp = clientIp;
        tokenRecord.ReasonRevoked = "Replaced by new token";

        var newRefreshToken = await tokenService.GenerateRefreshTokenAsync(user.Id, clientIp);
        tokenRecord.ReplacedByToken = newRefreshToken.Token;
        await dbContext.SaveChangesAsync();

        // Cấp Access Token mới
        var roles = await userManager.GetRolesAsync(user);
        var (newAccessToken, accessExpires) = tokenService.CreateToken(user, roles);

        return Ok(new AuthResponse(
            Token: newAccessToken,
            Email: user.Email!,
            FullName: user.FullName,
            UserId: user.Id,
            ExpiresAt: accessExpires,
            Roles: [.. roles],
            Avatar: user.Avatar,
            Status: user.Status.ToString(),
            RefreshToken: newRefreshToken.Token,
            RefreshTokenExpiresAt: newRefreshToken.ExpiresAt
        ));
    }

    // ==========================================
    // UC-6: Đăng xuất (Logout: Revoke Refresh Token & Blacklist Access Token)
    // ==========================================
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
    {
        var clientIp = GetClientIp();

        // 1. Thu hồi Refresh Token trong DB nếu được truyền lên
        if (!string.IsNullOrEmpty(request?.RefreshToken))
        {
            var tokenRecord = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == request.RefreshToken);

            if (tokenRecord is not null && !tokenRecord.IsRevoked)
            {
                tokenRecord.RevokedAt = Common.TimeHelper.Now;
                tokenRecord.RevokedByIp = clientIp;
                tokenRecord.ReasonRevoked = "Revoked via Logout";
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Refresh token revoked for User {UserId}", tokenRecord.UserId);
            }
        }

        // 2. Thu hồi Access Token hiện tại qua Redis Blacklist (nếu có Authorization header)
        var authHeader = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var tokenStr = authHeader["Bearer ".Length..].Trim();
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(tokenStr))
            {
                var jwt = handler.ReadJwtToken(tokenStr);
                var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;

                if (!string.IsNullOrEmpty(jti) && long.TryParse(expClaim, out var expUnix))
                {
                    var expUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                    var remaining = expUtc - DateTime.UtcNow;

                    if (remaining > TimeSpan.Zero)
                    {
                        await blacklistService.BlacklistTokenAsync(jti, remaining);
                    }
                }
            }
        }

        return Ok(new { message = "Đăng xuất thành công. Phiên làm việc đã bị hủy." });
    }

    // ==========================================
    // UC-7.1: Xem thông tin cá nhân (Profile)
    // ==========================================
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        if (userId is null) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var roles = await userManager.GetRolesAsync(user);

        return Ok(new UserProfileDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FullName: user.FullName,
            Avatar: user.Avatar,
            Status: user.Status.ToString(),
            Roles: [.. roles],
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt
        ));
    }
}
