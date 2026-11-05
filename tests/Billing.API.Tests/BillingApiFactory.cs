using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Billing.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Billing.API.Tests;

public class BillingApiFactory : WebApplicationFactory<Program>
{
    // Near real time so JWT lifetime validation (system clock) and business time agree.
    public FixedClock Clock { get; } = new(DateTimeOffset.UtcNow);
    // Test-only ai-service simulator; null leaves AiService:BaseUrl empty (AI not deployed).
    public HttpMessageHandler? AiHandler { get; set; }
    // Test-only Identity Admin API simulator (GET /api/users/{id}); defaults to an active Learner.
    public FakeIdentity Identity { get; } = new();
    public string VnpayTmnCode { get; init; } = "TESTTMN1";
    public string VnpayHashSecret { get; init; } = "test-only-hash-secret";
    // Test-only service overrides applied last (e.g. a revenue cache over in-memory storage).
    public Action<IServiceCollection>? ExtraServices { get; set; }
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public BillingApiFactory() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=127.0.0.1,1;Database=BillingTests;Integrated Security=True;Connect Timeout=1",
            ["Jwt:PublicKey"] = signingKey.ExportSubjectPublicKeyInfoPem(),
            ["Jwt:PublicKeyPath"] = "",
            ["AiService:BaseUrl"] = AiHandler is null ? "" : "http://ai.test/",
            ["Identity:BaseUrl"] = "http://identity.test/",
            // Workers are exercised directly by tests; never touch a real broker from the test host.
            ["BackgroundJobs:Enabled"] = "false",
            // Test-only VNPay merchant values (never the real sandbox credentials).
            ["Vnpay:TmnCode"] = VnpayTmnCode,
            ["Vnpay:HashSecret"] = VnpayHashSecret,
            ["Vnpay:ReturnUrl"] = "http://gateway.test/billing/api/payments/vnpay-return",
            ["Frontend:PaymentResultUrl"] = "http://storefront.test/Billing/PaymentResult"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<BillingDbContext>();
            services.RemoveAll<DbContextOptions<BillingDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<BillingDbContext>>();
            services.AddScoped<BillingDbContext>(_ => new SqliteBillingDbContext(
                new DbContextOptionsBuilder<BillingDbContext>().UseSqlite(connection).Options));
            services.AddHttpClient<Billing.API.Services.Interfaces.IIdentityUserClient, Billing.API.Services.IdentityUserClient>()
                .ConfigurePrimaryHttpMessageHandler(() => Identity);
            if (AiHandler is not null)
                services.AddHttpClient<Billing.API.Services.Interfaces.IAiUsageClient, Billing.API.Services.AiUsageClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => AiHandler);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddControllers().AddApplicationPart(typeof(AuthenticationProbeController).Assembly);
            ExtraServices?.Invoke(services);
        });
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Critical));
    }

    public async Task WithDb(Func<BillingDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await db.Database.EnsureCreatedAsync(); // Test-owned in-memory SQLite only.
        await action(db);
    }

    public string Token(string? role = "Learner", string? subject = null, string? issuer = null,
        string? audience = null, bool expired = false, bool wrongKey = false, string? email = null)
    {
        using var otherKey = RSA.Create(2048);
        var claims = new List<Claim> { new("sub", subject ?? Guid.NewGuid().ToString()) };
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        if (email is not null) claims.Add(new Claim("email", email));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer ?? "ToeicLearning.Identity", audience: audience ?? "ToeicLearning.Clients",
            claims: claims, notBefore: DateTime.UtcNow.AddHours(-2),
            expires: expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(wrongKey ? otherKey : signingKey)
                { KeyId = "acadprep-rsa-key-1" }, SecurityAlgorithms.RsaSha256)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { connection.Dispose(); signingKey.Dispose(); }
    }
}

// SQLite stores decimal as TEXT; use numeric representation for relational test constraints.
// Production remains SQL Server decimal(18,0), verified separately in the model test.
internal class SqliteBillingDbContext(DbContextOptions<BillingDbContext> options) : BillingDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Billing.API.Entities.Plan>().Property(plan => plan.Price).HasConversion<double>();
        modelBuilder.Entity<Billing.API.Entities.Order>().Property(order => order.Amount).HasConversion<double>();
    }
}

// Test-only deterministic clock.
public class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

// Test-only Identity simulator; production calls Identity's real Admin API.
public class FakeIdentity : HttpMessageHandler
{
    public Dictionary<Guid, (int Status, string[] Roles, string UserStatus, string? Email)> Users { get; } = new();
    public int? ForceStatus { get; set; }
    public bool Throw { get; set; }
    public List<(string Path, string? Token)> Calls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        Calls.Add((path, request.Headers.Authorization?.Parameter));
        if (Throw) throw new HttpRequestException("Simulated Identity outage.");
        if (ForceStatus is { } forced) return Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)forced));
        var id = Guid.Parse(path.Split('/').Last());
        (int Status, string[] Roles, string UserStatus, string? Email) user =
            Users.TryGetValue(id, out var configured) ? configured : (200, ["Learner"], "Active", $"{id:N}@example.test");
        if (user.Status != 200) return Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)user.Status));
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                id = id.ToString(), email = user.Email, fullName = "Test", status = user.UserStatus, roles = user.Roles
            })
        });
    }

    protected override void Dispose(bool disposing) { }
}
