using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Billing.API.Data;
using Billing.API.Entities;
using Billing.API.Services;
using Xunit;
using static Billing.API.Tests.BillingTestData;

namespace Billing.API.Tests;

public class UsageTests
{
    private const string Url = "/api/subscriptions/me/usage";

    [Fact]
    public async Task AnonymousIsUnauthorized()
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client(null);
        using var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FreeUsesPersistedLimitsAndReportsUsageUnavailableNotZero()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(Seed);
        using var client = factory.Client("Learner");
        var body = await Get(client);
        Assert.Equal("FREE", body.GetProperty("planCode").GetString());
        var features = body.GetProperty("features").EnumerateArray().ToDictionary(f => f.GetProperty("feature").GetString()!);
        Assert.Equal(new[] { "FullTest", "Practice", "Vocab" }, features.Keys.ToArray());
        Assert.Equal(3, features["FullTest"].GetProperty("limitValue").GetInt32());
        Assert.Equal("Monthly", features["FullTest"].GetProperty("period").GetString());
        Assert.Equal(5, features["Practice"].GetProperty("limitValue").GetInt32());
        Assert.Equal(100, features["Vocab"].GetProperty("limitValue").GetInt32());
        foreach (var feature in features.Values)
        {
            Assert.False(feature.GetProperty("usageAvailable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, feature.GetProperty("used").ValueKind);
            Assert.Equal(JsonValueKind.Null, feature.GetProperty("remaining").ValueKind);
        }
        Assert.Equal(JsonValueKind.Null, features["Vocab"].GetProperty("resetAt").ValueKind);
        var now = factory.Clock.Now.UtcDateTime;
        Assert.Equal(UsagePeriods.ResetAt("Daily", now), features["Practice"].GetProperty("resetAt").GetDateTime().ToUniversalTime());
        Assert.Equal(UsagePeriods.ResetAt("Monthly", now), features["FullTest"].GetProperty("resetAt").GetDateTime().ToUniversalTime());
        // AI not deployed (no BaseUrl): unavailable, never zero.
        var ai = body.GetProperty("ai");
        Assert.False(ai.GetProperty("available").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ai.GetProperty("tokensUsed").ValueKind);
    }

    [Fact]
    public async Task ActiveProShowsUnlimitedAndExpiredFallsBackToFree()
    {
        using var factory = new BillingApiFactory();
        var pro = Guid.NewGuid();
        var expired = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            await Seed(db);
            db.Subscriptions.Add(Subscription(pro, now.AddDays(-1), now.AddDays(5)));
            db.Subscriptions.Add(Subscription(expired, now.AddDays(-30), now)); // exactly at EndDate
            await db.SaveChangesAsync();
        });
        using var proClient = factory.Client("Learner", pro);
        var proBody = await Get(proClient);
        Assert.Equal("PRO_MONTH", proBody.GetProperty("planCode").GetString());
        Assert.All(proBody.GetProperty("features").EnumerateArray(),
            f => Assert.Equal(JsonValueKind.Null, f.GetProperty("limitValue").ValueKind));
        using var expiredClient = factory.Client("Learner", expired);
        Assert.Equal("FREE", (await Get(expiredClient)).GetProperty("planCode").GetString());
    }

    [Fact]
    public async Task MissingLimitRowIs503NotUnlimited()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.PlanLimits.Add(new PlanLimit { PlanId = FreeId, Feature = "FullTest", LimitValue = 3, Period = "Monthly" });
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner");
        using var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AiUsageIsReadWithCallersJwtAndMapped()
    {
        var ai = new FakeAi { Body = "{\"tokenLimit\":5000,\"tokensUsed\":1200,\"remaining\":3800,\"periodKey\":\"202611\",\"resetAt\":\"2026-11-30T17:00:00Z\"}" };
        using var factory = new BillingApiFactory { AiHandler = ai };
        await factory.WithDb(Seed);
        var user = Guid.NewGuid();
        using var client = factory.Client("Learner", user);
        var body = (await Get(client)).GetProperty("ai");
        Assert.True(body.GetProperty("available").GetBoolean());
        Assert.Equal(5000, body.GetProperty("tokenLimit").GetInt64());
        Assert.Equal(1200, body.GetProperty("tokensUsed").GetInt64());
        Assert.Equal(3800, body.GetProperty("remaining").GetInt64());
        Assert.Equal(new DateTime(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc), body.GetProperty("resetAt").GetDateTime().ToUniversalTime());
        Assert.Equal("http://ai.test/api/ai/usage/me", ai.Uri);
        Assert.Equal(client.DefaultRequestHeaders.Authorization!.Parameter, ai.Token);
    }

    [Theory]
    [InlineData("500")]
    [InlineData("401")]
    [InlineData("timeout")]
    [InlineData("network")]
    [InlineData("badjson")]
    [InlineData("missingfield")]
    [InlineData("negative")]
    public async Task AiFailureStillReturns200WithAiUnavailable(string mode)
    {
        var ai = new FakeAi
        {
            Status = mode switch { "500" => 500, "401" => 401, _ => 200 },
            Body = mode switch
            {
                "badjson" => "not-json", "missingfield" => "{\"tokenLimit\":1}",
                "negative" => "{\"tokensUsed\":-1}", _ => "{\"tokensUsed\":1}"
            },
            Throw = mode switch
            {
                "timeout" => new TaskCanceledException("Simulated timeout."),
                "network" => new HttpRequestException("Simulated network failure."), _ => null
            }
        };
        using var factory = new BillingApiFactory { AiHandler = ai };
        await factory.WithDb(Seed);
        using var client = factory.Client("Learner");
        var body = await Get(client);
        Assert.False(body.GetProperty("ai").GetProperty("available").GetBoolean());
        Assert.Equal(3, body.GetProperty("features").GetArrayLength());
    }

    private static async Task Seed(BillingDbContext db)
    {
        await SeedPlans(db);
        // Test fixture values mirror the approved Free quotas; Pro values are explicit unlimited rows.
        db.PlanLimits.AddRange(
            new PlanLimit { PlanId = FreeId, Feature = "FullTest", LimitValue = 3, Period = "Monthly" },
            new PlanLimit { PlanId = FreeId, Feature = "Practice", LimitValue = 5, Period = "Daily" },
            new PlanLimit { PlanId = FreeId, Feature = "Vocab", LimitValue = 100, Period = "Lifetime" },
            new PlanLimit { PlanId = MonthId, Feature = "FullTest", LimitValue = null, Period = "Monthly" },
            new PlanLimit { PlanId = MonthId, Feature = "Practice", LimitValue = null, Period = "Daily" },
            new PlanLimit { PlanId = MonthId, Feature = "Vocab", LimitValue = null, Period = "Lifetime" });
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> Get(HttpClient client)
    {
        using var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed class FakeAi : HttpMessageHandler
    {
        public int Status { get; init; } = 200;
        public string Body { get; init; } = "{}";
        public Exception? Throw { get; init; }
        public string? Uri { get; private set; }
        public string? Token { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri = request.RequestUri!.ToString();
            Token = request.Headers.Authorization?.Parameter;
            if (Throw is not null) throw Throw;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)Status)
                { Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json") });
        }

        protected override void Dispose(bool disposing) { }
    }
}
