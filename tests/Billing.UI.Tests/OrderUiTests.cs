using System.Net;
using Xunit;

namespace Billing.UI.Tests;

public class OrderUiTests
{
    [Theory]
    [InlineData("/Billing/Orders")]
    [InlineData("/Billing/Orders/Details?orderCode=ORD123")]
    [InlineData("/Billing/Subscription")]
    public async Task AdminAndModeratorCannotOpenLearnerPages(string url)
    {
        foreach (var role in new[] { "Admin", "Moderator" })
        {
            using var factory = new BillingUiFactory();
            factory.Backend.Roles = [role];
            using var client = factory.Browser();
            await factory.Login(client);
            using var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain(factory.Backend.Calls, call => call.Path.StartsWith("/billing/api/orders") ||
                call.Path.StartsWith("/billing/api/subscriptions"));
        }
    }

    [Fact]
    public async Task OrderListRendersEncodedApiDataWithStatusFilter()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync("/Billing/Orders?status=Expired");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("ORD123", html);
        Assert.Contains("Hết hạn", html);
        Assert.Contains("79.000 đ", html);
        Assert.Contains("19/10/2026 00:30", html);
        Assert.Contains("&lt;i&gt;Pro&lt;/i&gt;", raw);
        Assert.Contains(factory.Backend.Calls, call => call.Path == "/billing/api/orders/me?status=Expired&pageIndex=1" && call.Bearer);
    }

    [Fact]
    public async Task EmptyAndFailedListsAreDistinct()
    {
        using var factory = new BillingUiFactory();
        factory.Backend.EmptyOrders = true;
        using var client = factory.Browser();
        await factory.Login(client);
        Assert.Contains("Chưa có đơn hàng", WebUtility.HtmlDecode(await client.GetStringAsync("/Billing/Orders")));
        factory.Backend.OrdersHttpStatus = 500;
        using var failed = await client.GetAsync("/Billing/Orders");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        var html = WebUtility.HtmlDecode(await failed.Content.ReadAsStringAsync());
        Assert.Contains("Chưa thể tải lịch sử đơn hàng", html);
        Assert.DoesNotContain("sensitive-test-diagnostic", html);
    }

    [Fact]
    public async Task DetailShowsStatusFromApiAndMissingOrderIs404()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Billing/Orders/Details?orderCode=ORD123"));
        Assert.Contains("Đã thanh toán", html);
        Assert.Contains("19/10/2026 00:45", html);
        using var missing = await client.GetAsync("/Billing/Orders/Details?orderCode=OTHER9");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("Không tìm thấy đơn hàng", WebUtility.HtmlDecode(await missing.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task AdminListForwardsFiltersAndPagesWithThem()
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        var raw = await client.GetStringAsync("/Admin/Billing/Orders?Filter.Status=Paid&Filter.PlanCode=PRO_MONTH&Filter.From=2026-10-01&Filter.To=2026-10-31&Filter.SortBy=amount&Filter.SortDir=asc");
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("ADM1", html);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;@example.test", raw);
        Assert.Contains("Trang 1 / 3", html);
        Assert.Contains("/billing/api/admin/orders?status=Paid&planCode=PRO_MONTH&from=2026-10-01&to=2026-10-31&sortBy=amount&sortDir=asc&pageIndex=1",
            factory.Backend.AdminOrderQueries);
        Assert.Contains("Filter.PageIndex=2", html);
        Assert.Contains("Filter.Status=Paid", html);
        Assert.Contains("data-val-billingdaterange", raw);
    }

    [Fact]
    public async Task InvalidDateRangeIsRejectedBeforeCallingBilling()
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Billing/Orders?Filter.From=2026-10-20&Filter.To=2026-10-19"));
        Assert.Contains("Đến ngày phải bằng hoặc sau Từ ngày", html);
        Assert.Empty(factory.Backend.AdminOrderQueries);
    }

    [Fact]
    public async Task CsvHandlerDownloadsTheMonthFileThroughBilling()
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync("/Admin/Billing/Orders?handler=Csv&Filter.Month=2026-10&Filter.Status=Paid");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("orders-2026-10.csv", response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("A1", await response.Content.ReadAsStringAsync());
        Assert.Contains("/billing/api/admin/orders?status=Paid&month=2026-10", factory.Backend.AdminOrderQueries);
    }

    [Theory]
    [InlineData("", 200, "Chọn tháng cần xuất CSV")]
    [InlineData("2026-10", 422, "quá nhiều đơn")]
    [InlineData("2026-10", 500, "Không thể xuất CSV lúc này")]
    public async Task CsvProblemsRenderThePageWithoutLeakingDiagnostics(string month, int upstream, string expected)
    {
        using var factory = Admin();
        factory.Backend.CsvStatus = upstream;
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync($"/Admin/Billing/Orders?handler=Csv&Filter.Month={month}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(expected, html);
        Assert.DoesNotContain("sensitive-test-diagnostic", html);
    }

    private static BillingUiFactory Admin()
    {
        var factory = new BillingUiFactory();
        factory.Backend.Roles = ["Admin"];
        return factory;
    }
}
