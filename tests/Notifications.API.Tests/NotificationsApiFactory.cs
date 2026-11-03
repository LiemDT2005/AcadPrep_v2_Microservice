using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Notifications.API.Tests;

public class NotificationsApiFactory : WebApplicationFactory<Program>
{
    private readonly RSA signingKey = RSA.Create(2048);
    public string EnvironmentName { get; init; } = "Production";
    public Guid UserId { get; } = Guid.NewGuid();
    public const string Issuer = "ToeicLearning.Identity";
    public const string Audience = "ToeicLearning.Clients";
    public const string KeyId = "acadprep-rsa-key-1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:KeyId"] = KeyId,
                ["Jwt:PublicKeyPath"] = "",
                ["Jwt:PublicKey"] = signingKey.ExportSubjectPublicKeyInfoPem(),
                // No server is running here: startup, /health and tests must not connect.
                ["ConnectionStrings:Default"] = "Server=127.0.0.1,1;Database=NotificationsFoundationTests;Integrated Security=True;Connect Timeout=1;Encrypt=True"
            }));
        builder.ConfigureServices(services => services.AddControllers()
            .AddApplicationPart(typeof(FoundationTestController).Assembly));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Critical));
    }

    public string CreateToken(string? issuer = null, string? audience = null, DateTime? expires = null,
        IEnumerable<Claim>? claims = null, RSA? key = null, string algorithm = SecurityAlgorithms.RsaSha256)
    {
        var credentials = algorithm == SecurityAlgorithms.None ? null : new SigningCredentials(
            new RsaSecurityKey(key ?? signingKey) { KeyId = KeyId }, algorithm);
        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: claims ?? [new Claim("sub", UserId.ToString()), new Claim("email", "learner@example.test"),
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()), new Claim(ClaimTypes.Role, "Learner")],
            notBefore: DateTime.UtcNow.AddHours(-1),
            expires: expires ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) signingKey.Dispose();
    }
}
