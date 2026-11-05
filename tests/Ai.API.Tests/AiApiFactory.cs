using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Ai.API.Data;
using Ai.API.Entities;
using Ai.API.Services;
using Ai.API.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Ai.API.Tests;

public class AiApiFactory : WebApplicationFactory<Program>
{
    public FixedClock Clock { get; } = new(DateTimeOffset.UtcNow);
    public FakeGemini Gemini { get; } = new();
    public FakeBilling Billing { get; } = new();
    public string GeminiKey { get; init; } = "test-only-gemini-key";
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public AiApiFactory() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=127.0.0.1,1;Database=AiTests;Integrated Security=True;Connect Timeout=1",
            ["Jwt:PublicKey"] = signingKey.ExportSubjectPublicKeyInfoPem(),
            ["Jwt:PublicKeyPath"] = "",
            ["Gemini:ApiKey"] = GeminiKey,
            ["Gemini:BaseUrl"] = "http://gemini.test/v1beta/",
            ["Gemini:RetryDelayMilliseconds"] = "0",
            ["Billing:BaseUrl"] = "http://billing.test/"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AiDbContext>();
            services.RemoveAll<DbContextOptions<AiDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AiDbContext>>();
            services.AddScoped(_ => new AiDbContext(new DbContextOptionsBuilder<AiDbContext>().UseSqlite(connection).Options));
            services.AddHttpClient<IAiClient, GeminiClient>().ConfigurePrimaryHttpMessageHandler(() => Gemini);
            services.AddHttpClient<IPlanResolver, BillingPlanClient>(client => client.BaseAddress = new Uri("http://billing.test/"))
                .ConfigurePrimaryHttpMessageHandler(() => Billing);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Critical));
    }

    public async Task WithDb(Func<AiDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        await db.Database.EnsureCreatedAsync(); // test-owned in-memory SQLite only
        await action(db);
    }

    // Approved values (Huy, 2026-11-05); tests may lower them to exercise boundaries.
    public Task SeedLimits(long free = 15_000, long pro = 60_000) => WithDb(async db =>
    {
        db.TokenLimits.AddRange(
            new AiTokenLimit { PlanCode = "FREE", TokenLimit = free, UpdatedAt = DateTime.UtcNow },
            new AiTokenLimit { PlanCode = "PRO_MONTH", TokenLimit = pro, UpdatedAt = DateTime.UtcNow },
            new AiTokenLimit { PlanCode = "PRO_QUARTER", TokenLimit = pro, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    });

    public HttpClient Client(string? role, Guid? subject = null)
    {
        var client = CreateClient();
        if (role is null) return client;
        var claims = new List<Claim> { new("sub", (subject ?? Guid.NewGuid()).ToString()), new(ClaimTypes.Role, role),
            new("email", "learner@example.test") };
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("ToeicLearning.Identity", "ToeicLearning.Clients",
            claims, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new RsaSecurityKey(signingKey) { KeyId = "acadprep-rsa-key-1" }, SecurityAlgorithms.RsaSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { connection.Dispose(); signingKey.Dispose(); }
    }
}

public class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

// Test-only Gemini simulator (countTokens + generateContent); production calls the real API.
public class FakeGemini : HttpMessageHandler
{
    public int? InputTokens { get; set; } = 150;
    public int CountStatus { get; set; } = 200;
    public string Reply { get; set; } = "**Present perfect** dùng cho hành động có liên hệ tới hiện tại.";
    public int TotalTokens { get; set; } = 900;
    public bool Blocked { get; set; }
    public Queue<int> GenerateStatuses { get; } = new();
    public ConcurrentQueue<(string Method, string Body, string? ApiKey)> Calls { get; } = new();
    public int GenerateCalls => Calls.Count(call => call.Method == "generateContent");
    public int CountCalls => Calls.Count(call => call.Method == "countTokens");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        var method = path[(path.LastIndexOf(':') + 1)..];
        Calls.Enqueue((method, await request.Content!.ReadAsStringAsync(ct),
            request.Headers.TryGetValues("x-goog-api-key", out var key) ? key.Single() : null));
        if (method == "countTokens")
            return CountStatus != 200 || InputTokens is null
                ? new HttpResponseMessage((HttpStatusCode)(CountStatus == 200 ? 500 : CountStatus))
                : Json(new { totalTokens = InputTokens });
        var status = GenerateStatuses.Count > 0 ? GenerateStatuses.Dequeue() : 200;
        if (status != 200) return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("{\"error\":\"sensitive\"}") };
        return Blocked
            ? Json(new { promptFeedback = new { blockReason = "SAFETY" }, usageMetadata = new { totalTokenCount = TotalTokens } })
            : Json(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = Reply } } }, finishReason = "STOP" } },
                usageMetadata = new { promptTokenCount = 100, candidatesTokenCount = 50, totalTokenCount = TotalTokens }
            });
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    protected override void Dispose(bool disposing) { }
}

// Test-only Billing simulator (GET /api/subscriptions/me).
public class FakeBilling : HttpMessageHandler
{
    public string PlanCode { get; set; } = "FREE";
    public int Status { get; set; } = 200;
    public List<string?> Tokens { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Tokens.Add(request.Headers.Authorization?.Parameter);
        if (Status != 200) return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Status));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { planCode = PlanCode, status = "Active" }) });
    }

    protected override void Dispose(bool disposing) { }
}

internal static class ChatHelpers
{
    public static async Task<(HttpStatusCode Status, JsonElement Body)> Chat(HttpClient client, string message,
        Guid? requestId = null, Guid? sessionId = null)
    {
        using var response = await client.PostAsJsonAsync("/api/ai/chat",
            new { requestId = requestId ?? Guid.NewGuid(), sessionId, message });
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }
}
