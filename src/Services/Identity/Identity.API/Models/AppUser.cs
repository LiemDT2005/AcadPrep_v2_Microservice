using Microsoft.AspNetCore.Identity;

namespace Identity.API.Models;

public class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
    public string AuthProvider { get; set; } = "Local";
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = Common.TimeHelper.Now;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
