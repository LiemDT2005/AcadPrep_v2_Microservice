using Identity.API.Models;

namespace Identity.API.IServices;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(AppUser user, IEnumerable<string> roles);
    Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string? ipAddress = null);
}
