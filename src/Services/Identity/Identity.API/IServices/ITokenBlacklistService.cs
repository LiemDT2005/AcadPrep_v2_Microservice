namespace Identity.API.IServices;

public interface ITokenBlacklistService
{
    Task BlacklistTokenAsync(string jti, TimeSpan remainingLifetime);
    Task<bool> IsBlacklistedAsync(string jti);
    Task BlacklistUserAsync(string userId, TimeSpan remainingLifetime);
    Task<bool> IsUserBlacklistedAsync(string userId);
    Task UnblacklistUserAsync(string userId);
}
