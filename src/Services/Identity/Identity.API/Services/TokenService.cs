using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.API.IRepositories;
using Identity.API.IServices;
using Identity.API.Models;
using Microsoft.IdentityModel.Tokens;

namespace Identity.API.Services;

public class TokenService : ITokenService
{
    private const string KeyId = "acadprep-rsa-key-1";
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public TokenService(
        IConfiguration configuration,
        IWebHostEnvironment env,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _configuration = configuration;
        _env = env;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(AppUser user, IEnumerable<string> roles)
    {
        var jwt = _configuration.GetSection("Jwt");
        var expireHours = double.Parse(jwt["ExpireHours"] ?? "8");
        var expiresUtc = DateTime.UtcNow.AddHours(expireHours);

        var keyPath = jwt["PrivateKeyPath"];
        string pemContent;
        if (!string.IsNullOrEmpty(keyPath))
        {
            var fullPath = Path.IsPathRooted(keyPath) ? keyPath : Path.Combine(_env.ContentRootPath, keyPath);
            pemContent = File.Exists(fullPath) ? File.ReadAllText(fullPath) : (jwt["PrivateKey"] ?? string.Empty);
        }
        else
        {
            pemContent = jwt["PrivateKey"] ?? string.Empty;
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(pemContent);
        var rsaKey = new RsaSecurityKey(rsa) { KeyId = KeyId };

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.FullName),
            new(ClaimTypes.NameIdentifier, user.Id),
            new("status", user.Status.ToString())
        };

        if (!string.IsNullOrEmpty(user.Avatar))
        {
            claims.Add(new Claim("avatar", user.Avatar));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var creds = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expiresUtc,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), Common.TimeHelper.Now.AddHours(expireHours));
    }

    public async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string? ipAddress = null)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(randomBytes);
        var expiresDays = double.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");

        var refreshToken = new RefreshToken
        {
            Token = token,
            UserId = userId,
            ExpiresAt = Common.TimeHelper.Now.AddDays(expiresDays),
            CreatedAt = Common.TimeHelper.Now,
            CreatedByIp = ipAddress
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        await _refreshTokenRepository.SaveChangesAsync();

        return refreshToken;
    }
}
