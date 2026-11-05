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
    public FakeIdentity Identity { get; } = new();
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
                ["ConnectionStrings:Default"] = "Server=127.0.0.1,1;Database=NotificationsFoundationTests;Integrated Security=True;Connect Timeout=1;Encrypt=True",
                ["Identity:BaseUrl"] = "http://identity.test/",
                // The reminder subscriber is exercised directly by tests; never touch a real broker.
                ["BackgroundJobs:Enabled"] = "false"
            }));
        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(FoundationTestController).Assembly);
            // Test-only Identity simulator; production calls Identity's real Admin API.
            services.AddHttpClient<Notifications.API.Services.Interfaces.IIdentityUserClient,
                Notifications.API.Services.IdentityUserClient>().ConfigurePrimaryHttpMessageHandler(() => Identity);
        });
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

// Test-only Identity Admin API simulator (GET /api/users/{id}): every user exists unless listed as missing.
public class FakeIdentity : HttpMessageHandler
{
    public HashSet<Guid> Missing { get; } = [];
    public int? ForceStatus { get; set; }
    public bool Throw { get; set; }
    public List<(string Path, string? Token)> Calls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        lock (Calls) Calls.Add((path, request.Headers.Authorization?.Parameter));
        if (Throw) throw new HttpRequestException("Simulated Identity outage.");
        if (ForceStatus is { } forced) return Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)forced));
        var id = Guid.Parse(path.Split('/').Last());
        return Task.FromResult(Missing.Contains(id)
            ? new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
            : new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { id = id.ToString(), roles = new[] { "Learner" }, status = "Banned" })
            });
    }

    protected override void Dispose(bool disposing) { }
}
