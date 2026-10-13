namespace Identity.API.Services;

public interface ITokenBlacklistService
{
    Task BlacklistTokenAsync(string jti, TimeSpan remainingLifetime);
    Task<bool> IsBlacklistedAsync(string jti);
}
