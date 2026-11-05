using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Billing.API.Tests.BillingTestData;

namespace Billing.API.Tests;

public class GrantTests
{
    private const string Url = "/api/admin/subscriptions/grant";

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Learner", 403)]
    [InlineData("Moderator", 403)]
    public async Task OnlyAdminCanGrant(string? role, int expected)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client(role);
        using var response = await client.PostAsJsonAsync(Url, Body(Guid.NewGuid()));
        Assert.Equal(expected, (int)response.StatusCode);
        await factory.WithDb(async db => Assert.Equal(0, await db.Subscriptions.CountAsync()));
    }

    [Fact]
    public async Task NewGrantStartsNowUsesPlanDurationAndAuditsWithoutOrder()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        var admin = Guid.NewGuid();
        var user = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        using var client = factory.Client("Admin", admin);
        using var response = await client.PostAsJsonAsync(Url, Body(user, note: "  support case  "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(now, body.GetProperty("startDate").GetDateTime().ToUniversalTime(), TimeSpan.FromMilliseconds(1));
        Assert.Equal(now.AddDays(30), body.GetProperty("endDate").GetDateTime().ToUniversalTime(), TimeSpan.FromMilliseconds(1));
        await factory.WithDb(async db =>
        {
            var subscription = await db.Subscriptions.SingleAsync();
            Assert.Equal(body.GetProperty("subscriptionId").GetGuid(), subscription.Id);
            Assert.Equal(user, subscription.UserId);
            Assert.Equal(admin, subscription.GrantedBy);
            Assert.Null(subscription.SourceOrderId);
            Assert.Equal("support case", subscription.Note);
            var grant = await db.SubscriptionGrants.SingleAsync();
            Assert.Equal(admin, grant.AdminId);
            Assert.Null(grant.PreviousEndDate);
            Assert.Equal(30, grant.DurationDays);
        });
    }

    [Fact]
    public async Task ActiveSubscriptionIsExtendedFromItsCurrentEndDate()
    {
        using var factory = new BillingApiFactory();
        var user = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        var existing = Subscription(user, now.AddDays(-20), now.AddDays(10));
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(existing);
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Admin");
        using var response = await client.PostAsJsonAsync(Url, Body(user, QuarterId, durationDays: 7));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await factory.WithDb(async db =>
        {
            var subscription = await db.Subscriptions.SingleAsync();
            Assert.Equal(existing.Id, subscription.Id);
            Assert.Equal(now.AddDays(17), subscription.EndDate, TimeSpan.FromMilliseconds(1));
            Assert.Equal(now.AddDays(-20), subscription.StartDate, TimeSpan.FromMilliseconds(1));
            Assert.Equal(QuarterId, subscription.PlanId);
            var grant = await db.SubscriptionGrants.SingleAsync();
            Assert.Equal(now.AddDays(10), grant.PreviousEndDate!.Value, TimeSpan.FromMilliseconds(1));
        });
    }

    [Fact]
    public async Task ExpiredActiveRowIsClosedAndNewPeriodStartsNow()
    {
        using var factory = new BillingApiFactory();
        var user = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        var old = Subscription(user, now.AddDays(-40), now.AddDays(-10));
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(old);
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Admin");
        using var response = await client.PostAsJsonAsync(Url, Body(user));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await factory.WithDb(async db =>
        {
            Assert.Equal(SubscriptionStatus.Expired, (await db.Subscriptions.SingleAsync(s => s.Id == old.Id)).Status);
            var current = await db.Subscriptions.SingleAsync(s => s.Status == SubscriptionStatus.Active);
            Assert.Equal(now, current.StartDate, TimeSpan.FromMilliseconds(1));
            Assert.Equal(now.AddDays(30), current.EndDate, TimeSpan.FromMilliseconds(1));
        });
    }

    [Theory]
    [InlineData("free", 422)]
    [InlineData("inactive", 404)]
    [InlineData("missing", 404)]
    public async Task PlanMustBeAnExistingPaidPlanOnSale(string plan, int expected)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db, quarterActive: false));
        var planId = plan switch { "free" => FreeId, "inactive" => QuarterId, _ => Guid.NewGuid() };
        using var client = factory.Client("Admin");
        using var response = await client.PostAsJsonAsync(Url, Body(Guid.NewGuid(), planId));
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await factory.WithDb(async db => Assert.Equal(0, await db.SubscriptionGrants.CountAsync()));
    }

    [Theory]
    [InlineData("{\"requestId\":\"R\",\"userId\":\"U\",\"planId\":\"P\",\"durationDays\":0}")]
    [InlineData("{\"requestId\":\"R\",\"userId\":\"U\",\"planId\":\"P\",\"durationDays\":366}")]
    [InlineData("{\"requestId\":\"R\",\"userId\":\"00000000-0000-0000-0000-000000000000\",\"planId\":\"P\"}")]
    [InlineData("{\"userId\":\"U\",\"planId\":\"P\"}")]
    [InlineData("{\"requestId\":\"R\",\"planId\":\"P\"}")]
    [InlineData("{\"requestId\":\"R\",\"userId\":\"U\",\"planId\":\"P\",\"grantedBy\":\"U\"}")]
    [InlineData("{\"requestId\":\"R\",\"userId\":\"not-a-guid\",\"planId\":\"P\"}")]
    [InlineData("NOTE")]
    public async Task InvalidInputIs400WithoutWrites(string template)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        var json = template == "NOTE"
            ? JsonSerializer.Serialize(Body(Guid.NewGuid(), note: new string('x', 501)))
            : template.Replace("\"R\"", $"\"{Guid.NewGuid()}\"").Replace("\"U\"", $"\"{Guid.NewGuid()}\"")
                .Replace("\"P\"", $"\"{MonthId}\"");
        using var client = factory.Client("Admin");
        using var response = await client.PostAsync(Url, new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.WithDb(async db => Assert.Equal(0, await db.Subscriptions.CountAsync()));
    }

    [Fact]
    public async Task ReplayingTheSameRequestNeverExtendsTwice()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        var admin = Guid.NewGuid();
        var body = Body(Guid.NewGuid(), durationDays: 5);
        using var client = factory.Client("Admin", admin);
        using var first = await client.PostAsJsonAsync(Url, body);
        using var second = await client.PostAsJsonAsync(Url, body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        await factory.WithDb(async db =>
        {
            Assert.Equal(1, await db.SubscriptionGrants.CountAsync());
            Assert.Equal(factory.Clock.Now.UtcDateTime.AddDays(5), (await db.Subscriptions.SingleAsync()).EndDate,
                TimeSpan.FromMilliseconds(1));
        });
    }

    [Fact]
    public async Task ReusingRequestIdForDifferentGrantIsConflict()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        var requestId = Guid.NewGuid();
        using var client = factory.Client("Admin");
        using var first = await client.PostAsJsonAsync(Url, Body(Guid.NewGuid(), requestId: requestId));
        using var second = await client.PostAsJsonAsync(Url, Body(Guid.NewGuid(), requestId: requestId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await factory.WithDb(async db => Assert.Equal(1, await db.Subscriptions.CountAsync()));
    }

    internal static Dictionary<string, object?> Body(Guid userId, Guid? planId = null, int? durationDays = null,
        string? note = null, Guid? requestId = null) => new()
    {
        ["requestId"] = requestId ?? Guid.NewGuid(), ["userId"] = userId, ["planId"] = planId ?? MonthId,
        ["durationDays"] = durationDays, ["note"] = note
    };
}
