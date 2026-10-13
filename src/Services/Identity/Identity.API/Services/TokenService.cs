using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Identity.API.Data;
using Microsoft.IdentityModel.Tokens;

namespace Identity.API.Services;

public class TokenService(IConfiguration configuration, IWebHostEnvironment env, IdentityDbContext dbContext)
{
    private const string KeyId = "acadprep-rsa-key-1";

    public (string Token, DateTime ExpiresAt) CreateToken(AppUser user, IEnumerable<string> roles)
    {
        var jwt = configuration.GetSection("Jwt");
        var expireHours = double.Parse(jwt["ExpireHours"] ?? "8");
        var expiresUtc = DateTime.UtcNow.AddHours(expireHours);

        var keyPath = jwt["PrivateKeyPath"];
        string pemContent;
        if (!string.IsNullOrEmpty(keyPath))
        {
            var fullPath = Path.IsPathRooted(keyPath) ? keyPath : Path.Combine(env.ContentRootPath, keyPath);
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
        var expiresDays = double.Parse(configuration["Jwt:RefreshTokenExpireDays"] ?? "7");

        var refreshToken = new RefreshToken
        {
            Token = token,
            UserId = userId,
            ExpiresAt = Common.TimeHelper.Now.AddDays(expiresDays),
            CreatedAt = Common.TimeHelper.Now,
            CreatedByIp = ipAddress
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        return refreshToken;
    }
}

