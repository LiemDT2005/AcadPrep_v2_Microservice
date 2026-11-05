using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Billing.API.Entities;
using Xunit;
using static Billing.API.Tests.BillingTestData;
using static Billing.API.Tests.OrderTestData;
using static Billing.API.Tests.OrderTests;

namespace Billing.API.Tests;

public class RevenueTests
{
    private const string Url = "/api/admin/revenue";

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Learner", 403)]
    [InlineData("Moderator", 403)]
    public async Task OnlyAdminReadsRevenue(string? role, int expected)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client(role);
        using var response = await client.GetAsync(Url);
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact]
    public async Task OnlyPaidOrdersCountAsRevenueBucketedByVietnamDayWithZeroFill()
    {
        using var factory = await Seeded();
        using var client = factory.Client("Admin");
        var body = await Json(client, $"{Url}?from=2026-10-18&to=2026-10-20");
        var points = body.GetProperty("points").EnumerateArray().ToArray();
        Assert.Equal(new[] { "2026-10-18", "2026-10-19", "2026-10-20" }, points.Select(p => p.GetProperty("period").GetString()).ToArray());
        // P1 paid 17:30 UTC 18/10 = 00:30 19/10 VN -> 19/10; P2 paid 16:00 UTC 18/10 = 23:00 18/10 VN -> 18/10.
        Assert.Equal(new[] { 100m, 200m, 0m }, points.Select(p => p.GetProperty("revenue").GetDecimal()).ToArray());
        Assert.Equal(new[] { 1, 1, 0 }, points.Select(p => p.GetProperty("paidCount").GetInt32()).ToArray());
        var totals = body.GetProperty("totals");
        Assert.Equal(300m, totals.GetProperty("revenue").GetDecimal());
        Assert.Equal(points.Sum(p => p.GetProperty("revenue").GetDecimal()), totals.GetProperty("revenue").GetDecimal());
        // Counts by CreatedAt in range: 2 Paid, 1 Failed, 1 Pending live, 2 Expired (one stale Pending).
        Assert.Equal(2, totals.GetProperty("paidCount").GetInt32());
        Assert.Equal(1, totals.GetProperty("failedCount").GetInt32());
        Assert.Equal(1, totals.GetProperty("pendingCount").GetInt32());
        Assert.Equal(2, totals.GetProperty("expiredCount").GetInt32());
    }

    [Fact]
    public async Task MonthlyGroupingAndDefaultLast30Days()
    {
        using var factory = await Seeded();
        using var client = factory.Client("Admin");
        var monthly = await Json(client, $"{Url}?from=2026-09-15&to=2026-11-02&groupBy=month");
        Assert.Equal(new[] { "2026-09", "2026-10", "2026-11" },
            monthly.GetProperty("points").EnumerateArray().Select(p => p.GetProperty("period").GetString()).ToArray());
        // October (VN) contains P1 + P2 + OUT1 (paid 21/10, inside this wider range).
        Assert.Equal(5300m, monthly.GetProperty("points")[1].GetProperty("revenue").GetDecimal());

        var defaults = await Json(client, Url);
        var today = DateOnly.FromDateTime(factory.Clock.Now.UtcDateTime.AddHours(7));
        Assert.Equal(today.ToString("yyyy-MM-dd"), defaults.GetProperty("to").GetString());
        Assert.Equal(today.AddDays(-29).ToString("yyyy-MM-dd"), defaults.GetProperty("from").GetString());
        Assert.Equal(30, defaults.GetProperty("points").GetArrayLength());
        Assert.Equal("day", defaults.GetProperty("groupBy").GetString());
    }

    [Theory]
    [InlineData("from=2026-10-20&to=2026-10-19")]
    [InlineData("from=2025-01-01&to=2026-01-02")]             // 367 days
    [InlineData("from=2021-01-01&to=2026-01-31&groupBy=month")] // 61 months
    [InlineData("groupBy=week")]
    [InlineData("from=bad")]
    public async Task InvalidRangeIs400(string query)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client("Admin");
        using var response = await client.GetAsync($"{Url}?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("from=2025-01-01&to=2026-01-01")]             // exactly 366 days
    [InlineData("from=2021-02-01&to=2026-01-31&groupBy=month")] // exactly 60 months
    public async Task BoundaryRangesAreAccepted(string query)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client("Admin");
        using var response = await client.GetAsync($"{Url}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CsvExportsThePointsOfTheSameRange()
    {
        using var factory = await Seeded();
        using var client = factory.Client("Admin");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Url}?from=2026-10-18&to=2026-10-20");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("revenue-day-2026-10-18-2026-10-20.csv", response.Content.Headers.ContentDisposition?.FileNameStar);
        var lines = Encoding.UTF8.GetString((await response.Content.ReadAsByteArrayAsync())[3..])
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "period,revenue,paidCount", "2026-10-18,100,1", "2026-10-19,200,1", "2026-10-20,0,0" }, lines);
    }

    internal static async Task<BillingApiFactory> Seeded(BillingApiFactory? existing = null)
    {
        var factory = existing ?? new BillingApiFactory();
        var user = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            var p1 = Order(user, "P1", Utc(2026, 10, 18, 17, 0), amount: 200);
            p1.PaidAt = Utc(2026, 10, 18, 17, 30);
            var p2 = Order(user, "P2", Utc(2026, 10, 18, 15, 0), amount: 100);
            p2.PaidAt = Utc(2026, 10, 18, 16, 0);
            db.Orders.AddRange(p1, p2,
                Order(user, "F1", Utc(2026, 10, 19, 1, 0), OrderStatus.Failed, amount: 999),
                Order(user, "E1", Utc(2026, 10, 19, 2, 0), OrderStatus.Expired, amount: 999),
                Order(user, "S1", Utc(2026, 10, 19, 3, 0), OrderStatus.Pending, amount: 999, planCode: "PRO_QUARTER", expiresAt: Utc(2026, 10, 19, 3, 15)),
                Order(user, "L1", Utc(2026, 10, 20, 3, 0), OrderStatus.Pending, amount: 999, expiresAt: now.AddMinutes(10)),
                Order(user, "OUT1", Utc(2026, 10, 21, 0, 0), amount: 5000)); // outside range
            await db.SaveChangesAsync();
        });
        return factory;
    }

    private static DateTime Utc(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, DateTimeKind.Utc);
}
