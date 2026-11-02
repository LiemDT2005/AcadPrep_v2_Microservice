using System.Security.Claims;
using Identity.API.DTOs;
using Identity.API.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    private string? GetClientIp() =>
        HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var ip)
            ? ip.FirstOrDefault()
            : HttpContext.Connection.RemoteIpAddress?.ToString();

    // ==========================================
    // UC-1: Register
    // ==========================================
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (succeeded, error, response) = await _authService.RegisterAsync(request);
        if (!succeeded)
            return BadRequest(new { error });

        return Ok(response);
    }

    // ==========================================
    // UC-2: Verify OTP
    // ==========================================
    [HttpPost("verify-otp")]
    public async Task<ActionResult<AuthResponse>> VerifyOtp(VerifyOtpRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (succeeded, error, response) = await _authService.VerifyOtpAsync(request, GetClientIp());
        if (!succeeded)
            return BadRequest(new { error });

        return Ok(response);
    }

    // ==========================================
    // UC-2: Resend OTP
    // ==========================================
    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(ResendOtpRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (succeeded, error, message) = await _authService.ResendOtpAsync(request);
        if (!succeeded)
            return BadRequest(new { error });

        return Ok(new { message });
    }

    // ==========================================
    // UC-5.1: Login with Email/Password
    // ==========================================
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (succeeded, error, response, isLockedOut) = await _authService.LoginAsync(request, GetClientIp());
        if (!succeeded)
        {
            if (isLockedOut)
                return StatusCode(StatusCodes.Status423Locked, new { error });

            return BadRequest(new { error });
        }

        return Ok(response);
    }

    // ==========================================
    // UC-5.2: Google Login
    // ==========================================
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> LoginGoogle([FromBody] GoogleLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (succeeded, error, response) = await _authService.LoginGoogleAsync(request, GetClientIp());
        if (!succeeded)
            return BadRequest(new { error });

        return Ok(response);
    }

    // ==========================================
    // Token Refresh
    // ==========================================
    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (succeeded, error, response) = await _authService.RefreshTokenAsync(request, GetClientIp());
        if (!succeeded)
            return Unauthorized(new { error });

        return Ok(response);
    }

    // ==========================================
    // UC-6: Logout
    // ==========================================
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request = null)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        await _authService.LogoutAsync(authHeader, request?.RefreshToken, GetClientIp());
        return Ok(new { message = "Logged out successfully. Token has been revoked." });
    }

    // ==========================================
    // UC-7.1: View Profile
    // ==========================================
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "User identifier not found in token." });

        var (succeeded, error, profile) = await _authService.GetProfileAsync(userId);
        if (!succeeded)
            return NotFound(new { error });

        return Ok(profile);
    }

    // ==========================================
    // UC-7.2: Update Profile
    // ==========================================
    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "User identifier not found in token." });

        var (succeeded, error, profile) = await _authService.UpdateProfileAsync(userId, request);
        if (!succeeded)
            return BadRequest(new { error });

        return Ok(profile);
    }

    // ==========================================
    // UC-7.3: Change Password
    // ==========================================
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "User identity could not be verified." });

        var (succeeded, error) = await _authService.ChangePasswordAsync(userId, request);
        if (!succeeded)
            return BadRequest(new { error });

        return Ok(new { message = "Password changed successfully." });
    }

    // ==========================================
    // UC-8: Forgot Password
    // ==========================================
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (succeeded, error, message) = await _authService.ForgotPasswordAsync(request);
        if (!succeeded)
            return BadRequest(new { error });

        return Ok(new { message });
    }

    // ==========================================
    // UC-8: Reset Password
    // ==========================================
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (succeeded, error, message) = await _authService.ResetPasswordAsync(request);
        if (!succeeded)
            return BadRequest(new { error });

        return Ok(new { message });
    }
}
