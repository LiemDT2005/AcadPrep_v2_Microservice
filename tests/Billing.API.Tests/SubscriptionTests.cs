using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Billing.API.Entities;
using Xunit;
using static Billing.API.Tests.BillingTestData;

namespace Billing.API.Tests;

public class SubscriptionTests
{
    [Fact]
    public async Task AnonymousIsUnauthorized()
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client(null);
        using var response = await client.GetAsync("/api/subscriptions/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task AdminAndModeratorAreForbiddenBecauseEachRoleHasOneJob(string role)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client(role);
        foreach (var url in new[] { "/api/subscriptions/me", "/api/subscriptions/me/usage" })
        {
            using var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task NeverSubscribedLearnerIsFree()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client("Learner");
        var body = await Get(client);
        Assert.Equal("Free", body.GetProperty("status").GetString());
        Assert.Equal("FREE", body.GetProperty("planCode").GetString());
        Assert.Equal("Test free", body.GetProperty("planName").GetString());
        Assert.False(body.GetProperty("isPro").GetBoolean());
        foreach (var name in new[] { "startDate", "endDate", "daysRemaining" })
            Assert.Equal(JsonValueKind.Null, body.GetProperty(name).ValueKind);
    }

    [Fact]
    public async Task ActiveReturnsPlanDatesAndRoundsRemainingDaysUp()
    {
        using var factory = new BillingApiFactory();
        var user = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(Subscription(user, now.AddDays(-3), now.AddDays(1).AddHours(2)));
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner", user);
        var body = await Get(client);
        Assert.Equal("Active", body.GetProperty("status").GetString());
        Assert.Equal("PRO_MONTH", body.GetProperty("planCode").GetString());
        Assert.True(body.GetProperty("isPro").GetBoolean());
        Assert.Equal(2, body.GetProperty("daysRemaining").GetInt32());
        Assert.EndsWith("Z", body.GetProperty("endDate").GetString());
    }

    [Theory]
    [InlineData(0, SubscriptionStatus.Active)]   // exactly at EndDate: no longer effective
    [InlineData(-5, SubscriptionStatus.Active)]  // job has not marked it yet
    [InlineData(-5, SubscriptionStatus.Expired)]
    public async Task ExpiredFallsBackToFreeWithLatestEndDate(int endOffsetDays, string persisted)
    {
        using var factory = new BillingApiFactory();
        var user = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        var end = now.AddDays(endOffsetDays);
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(Subscription(user, end.AddDays(-30), end, persisted));
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner", user);
        var body = await Get(client);
        Assert.Equal("Expired", body.GetProperty("status").GetString());
        Assert.Equal("FREE", body.GetProperty("planCode").GetString());
        Assert.False(body.GetProperty("isPro").GetBoolean());
        Assert.Equal(0, body.GetProperty("daysRemaining").GetInt32());
        Assert.Equal(end, body.GetProperty("endDate").GetDateTime().ToUniversalTime(), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task OtherUsersSubscriptionIsNeverVisibleEvenWithQueryUserId()
    {
        using var factory = new BillingApiFactory();
        var other = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(Subscription(other, now, now.AddDays(30)));
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner");
        using var response = await client.GetAsync($"/api/subscriptions/me?userId={other}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Free", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MissingFreePlanIsConfigurationErrorNotHardcodedPlan()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db, includeFree: false));
        using var client = factory.Client("Learner");
        using var response = await client.GetAsync("/api/subscriptions/me");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<JsonElement> Get(HttpClient client)
    {
        using var response = await client.GetAsync("/api/subscriptions/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
