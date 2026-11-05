using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Billing.API.Entities;
using Xunit;
using static Billing.API.Tests.BillingTestData;
using static Billing.API.Tests.OrderTestData;

namespace Billing.API.Tests;

public class OrderTests
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Admin", 403)]
    [InlineData("Moderator", 403)]
    public async Task OnlyLearnersReadTheirOrders(string? role, int expected)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client(role);
        using var list = await client.GetAsync("/api/orders/me");
        using var detail = await client.GetAsync("/api/orders/ABC123");
        Assert.Equal(expected, (int)list.StatusCode);
        Assert.Equal(expected, (int)detail.StatusCode);
    }

    [Fact]
    public async Task ListShowsOnlyOwnOrdersNewestFirstWithPaging()
    {
        using var factory = new BillingApiFactory();
        var me = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Orders.AddRange(Order(me, "OLD1", now.AddDays(-3)), Order(me, "MID1", now.AddDays(-2)),
                Order(me, "NEW1", now.AddDays(-1)), Order(Guid.NewGuid(), "OTHER1", now));
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner", me);
        var page = await Json(client, "/api/orders/me?pageSize=2");
        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(new[] { "NEW1", "MID1" }, Codes(page));
        Assert.Equal(new[] { "OLD1" }, Codes(await Json(client, "/api/orders/me?pageSize=2&pageIndex=2")));
        var item = page.GetProperty("items")[0];
        Assert.Equal("Snapshot PRO_MONTH", item.GetProperty("planName").GetString());
        Assert.False(item.TryGetProperty("vnpTransactionNo", out _));
        Assert.False(item.TryGetProperty("userId", out _));
    }

    [Fact]
    public async Task PendingPastExpiryIsReadAsExpiredAndFiltersAgree()
    {
        using var factory = new BillingApiFactory();
        var me = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Orders.AddRange(
                Order(me, "LIVE1", now.AddMinutes(-5), OrderStatus.Pending, expiresAt: now.AddMinutes(10)),
                Order(me, "STALE1", now.AddMinutes(-30), OrderStatus.Pending, planCode: "PRO_QUARTER", expiresAt: now), // exactly at ExpiresAt
                Order(me, "EXP1", now.AddDays(-1), OrderStatus.Expired),
                Order(me, "PAID1", now.AddDays(-2)));
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner", me);
        Assert.Equal(new[] { "LIVE1" }, Codes(await Json(client, "/api/orders/me?status=Pending")));
        Assert.Equal(new[] { "STALE1", "EXP1" }, Codes(await Json(client, "/api/orders/me?status=Expired")));
        var detail = await Json(client, "/api/orders/STALE1");
        Assert.Equal("Expired", detail.GetProperty("status").GetString());
        Assert.True(detail.TryGetProperty("expiresAt", out _));
    }

    [Theory]
    [InlineData("status=Refunded")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    [InlineData("pageIndex=0")]
    public async Task InvalidQueryIs400(string query)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client("Learner");
        using var response = await client.GetAsync("/api/orders/me?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("OTHER1")]
    [InlineData("NOPE1")]
    [InlineData("bad%20code")]
    public async Task OtherUsersOrMissingOrMalformedCodeIs404(string code)
    {
        using var factory = new BillingApiFactory();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Orders.Add(Order(Guid.NewGuid(), "OTHER1", now));
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner");
        using var response = await client.GetAsync("/api/orders/" + code);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    internal static async Task<JsonElement> Json(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal static string[] Codes(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("orderCode").GetString()!).ToArray();
}
