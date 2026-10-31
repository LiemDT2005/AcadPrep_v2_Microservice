using Identity.API.Models;

namespace Identity.API.IRepositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<List<RefreshToken>> GetRecentTokensByUserIdAsync(string userId, int count = 5);
    Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(string userId);
    Task AddAsync(RefreshToken refreshToken);
    Task UpdateAsync(RefreshToken refreshToken);
    Task SaveChangesAsync();
}
