using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Billing.API.Entities;
using Xunit;
using static Billing.API.Tests.BillingTestData;
using static Billing.API.Tests.OrderTestData;
using static Billing.API.Tests.OrderTests;

namespace Billing.API.Tests;

public class AdminOrderTests
{
    private const string Url = "/api/admin/orders";
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Learner", 403)]
    [InlineData("Moderator", 403)]
    public async Task OnlyAdminCanListOrExport(string? role, int expected)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client(role);
        using var json = await client.GetAsync(Url);
        Assert.Equal(expected, (int)json.StatusCode);
        using var csv = await Csv(client, "?month=2026-10");
        Assert.Equal(expected, (int)csv.StatusCode);
    }

    [Fact]
    public async Task FiltersSortingAndPagingRunTogether()
    {
        using var factory = await Seeded();
        using var client = factory.Client("Admin");
        Assert.Equal(new[] { "A0", "A3", "A2", "A1", "B1" }, Codes(await Json(client, Url)));
        Assert.Equal(new[] { "A1", "A2", "A3", "A0" }, Codes(await Json(client, $"{Url}?userId={UserA}&sortDir=asc")));
        Assert.Equal(new[] { "B1", "A2" }, Codes(await Json(client, $"{Url}?sortBy=amount&sortDir=desc&pageSize=2")));
        Assert.Equal(new[] { "A2" }, Codes(await Json(client, $"{Url}?planCode=PRO_QUARTER")));
        Assert.Equal(new[] { "A3" }, Codes(await Json(client, $"{Url}?status=Failed")));
        var row = (await Json(client, $"{Url}?planCode=PRO_QUARTER")).GetProperty("items")[0];
        Assert.Equal("a@example.test", row.GetProperty("userEmail").GetString());
        Assert.Equal(UserA, row.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task FromToAreInclusiveVietnamCalendarDays()
    {
        using var factory = await Seeded();
        using var client = factory.Client("Admin");
        // A1 at 2026-10-18 17:30 UTC = 19/10 00:30 VN; B1 at 2026-10-18 16:30 UTC = 18/10 23:30 VN.
        Assert.Equal(new[] { "A1" }, Codes(await Json(client, $"{Url}?from=2026-10-19&to=2026-10-19")));
        Assert.Equal(new[] { "B1" }, Codes(await Json(client, $"{Url}?from=2026-10-18&to=2026-10-18")));
    }

    [Theory]
    [InlineData("status=Refunded")]
    [InlineData("planCode=GOLD")]
    [InlineData("sortBy=userEmail")]
    [InlineData("sortDir=up")]
    [InlineData("pageSize=101")]
    [InlineData("userId=00000000-0000-0000-0000-000000000000")]
    [InlineData("from=2026-10-20&to=2026-10-19")]
    [InlineData("from=not-a-date")]
    [InlineData("month=2026-10")] // month is a CSV-only parameter
    public async Task InvalidJsonQueryIs400(string query)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client("Admin");
        using var response = await client.GetAsync($"{Url}?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CsvExportsTheWholeVietnamMonthWithFiltersThroughTheCustomFormatter()
    {
        using var factory = await Seeded();
        using var client = factory.Client("Admin");
        using var response = await Csv(client, "?month=2026-10&pageSize=1&pageIndex=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("orders-2026-10.csv", response.Content.Headers.ContentDisposition?.FileNameStar);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]); // UTF-8 BOM for Excel
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("orderCode,userId,userEmail,planCode,amount,kind,status,createdAt,paidAt", lines[0]);
        // Paging is ignored; A1 (19/10 VN) and B1 (18/10 VN) are October; A0 (31/10 17:30 UTC = 01/11 VN) is not.
        Assert.Equal(new[] { "B1", "A1", "A2", "A3" }, lines.Skip(1).Select(line => line.Split(',')[0]).ToArray());
        Assert.Contains("'=HYPERLINK(1)@example.test", lines[1]);

        using var filtered = await Csv(client, $"?month=2026-10&userId={UserA}&status=Paid");
        var filteredLines = (await filtered.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(new[] { "A1", "A2" }, filteredLines.Skip(1).Select(line => line.Split(',')[0]).ToArray());
    }

    [Theory]
    [InlineData("", 422)]
    [InlineData("?month=2026-13", 400)]
    [InlineData("?month=2026-10&from=2026-10-01", 400)]
    public async Task CsvRequiresAValidMonthAndReturnsProblemJson(string query, int expected)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.Client("Admin");
        using var response = await Csv(client, query);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EmptyMonthIsHeaderOnlyNotAnError()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client("Admin");
        using var response = await Csv(client, "?month=2025-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
    }

    private static async Task<BillingApiFactory> Seeded()
    {
        var factory = new BillingApiFactory();
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Orders.AddRange(
                Order(UserA, "A0", Utc(2026, 10, 31, 17, 30), amount: 5),
                Order(UserA, "A1", Utc(2026, 10, 18, 17, 30), amount: 10, email: "a@example.test"),
                Order(UserA, "A2", Utc(2026, 10, 20, 0, 0), amount: 30, planCode: "PRO_QUARTER", email: "a@example.test"),
                Order(UserA, "A3", Utc(2026, 10, 21, 0, 0), OrderStatus.Failed, amount: 20),
                Order(Guid.NewGuid(), "B1", Utc(2026, 10, 18, 16, 30), amount: 40, email: "=HYPERLINK(1)@example.test"));
            await db.SaveChangesAsync();
        });
        return factory;
    }

    private static DateTime Utc(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

    private static async Task<HttpResponseMessage> Csv(HttpClient client, string query)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url + query);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
        return await client.SendAsync(request);
    }
}
