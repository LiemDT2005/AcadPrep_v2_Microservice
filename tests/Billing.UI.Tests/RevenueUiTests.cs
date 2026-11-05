using System.Net;
using Xunit;

namespace Billing.UI.Tests;

public class RevenueUiTests
{
    private const string Page = "/Admin/Billing/Revenue";

    [Theory]
    [InlineData("Learner")]
    [InlineData("Moderator")]
    public async Task OnlyAdminCanOpenRevenue(string role)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.Roles = [role];
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync(Page);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Backend.RevenueQueries);
    }

    [Fact]
    public async Task TotalsChartDataAndTableComeFromTheApiEncoded()
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        var raw = await client.GetStringAsync($"{Page}?Filter.From=2026-10-01&Filter.To=2026-10-30&Filter.GroupBy=day");
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("1.250.000 đ", html);
        Assert.Contains("billing-revenue-chart", raw);
        Assert.Contains("billing-revenue.js", raw);
        Assert.Contains("chart.js@4", raw);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", raw);          // table cell encoded
        Assert.DoesNotContain("\"<b>x</b>\"", raw);              // JSON island escaped
        Assert.Contains("value=\"2026-10-01\"", raw);
        Assert.Contains("data-val-billingdaterange", raw);
        Assert.Contains("/billing/api/admin/revenue?from=2026-10-01&to=2026-10-30&groupBy=day", factory.Backend.RevenueQueries);
    }

    [Fact]
    public async Task ZeroRevenueIsShownAsRealZero()
    {
        using var factory = Admin();
        factory.Backend.RevenueZero = true;
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(Page));
        Assert.Contains("0 đ", html);
        Assert.Contains("Cấp gói thủ công không tính vào doanh thu", html);
    }

    [Fact]
    public async Task InvalidDateRangeIsRejectedBeforeCallingBilling()
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"{Page}?Filter.From=2026-10-20&Filter.To=2026-10-19"));
        Assert.Contains("Đến ngày phải bằng hoặc sau Từ ngày", html);
        Assert.Empty(factory.Backend.RevenueQueries);
    }

    [Theory]
    [InlineData(400, "Khoảng thời gian không hợp lệ")]
    [InlineData(500, "Chưa thể tải thống kê doanh thu")]
    public async Task ApiErrorsAreFriendlyWithoutDiagnostics(int upstream, string expected)
    {
        using var factory = Admin();
        factory.Backend.RevenueStatus = upstream;
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(Page));
        Assert.Contains(expected, html);
        Assert.DoesNotContain("sensitive-test-diagnostic", html);
        Assert.DoesNotContain("billing-revenue-chart", html);
    }

    [Fact]
    public async Task CsvHandlerDownloadsThroughBillingWithTheSameRange()
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync($"{Page}?handler=Csv&Filter.From=2026-10-01&Filter.To=2026-10-30&Filter.GroupBy=day");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("revenue-day-2026-10-01-2026-10-30.csv", response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("period,revenue,paidCount", await response.Content.ReadAsStringAsync());
    }

    private static BillingUiFactory Admin()
    {
        var factory = new BillingUiFactory();
        factory.Backend.Roles = ["Admin"];
        return factory;
    }
}
