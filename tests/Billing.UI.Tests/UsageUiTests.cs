using System.Net;
using Xunit;

namespace Billing.UI.Tests;

public class UsageUiTests
{
    [Fact]
    public async Task PricingHidesUnimplementedFeatureQuotasAndKeepsSubscriptionAndAiState()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Billing/Plans"));
        Assert.DoesNotContain("id=\"billing-usage\"", html);
        Assert.DoesNotContain("3 / tháng", html);
        Assert.DoesNotContain("100 / tổng", html);
        Assert.DoesNotContain("Chưa có dữ liệu", html);
        Assert.DoesNotContain("luyện đề không giới hạn", html);
        Assert.DoesNotContain("Luyện thi không giới hạn", html);
        Assert.Contains("Đang hoạt động", html);
        Assert.Contains("Hạn mức token AI theo gói Pro", html);
        Assert.Contains("Chưa tải được dữ liệu AI", html);
        Assert.Contains(factory.Backend.Calls, call => call.Path == "/billing/api/subscriptions/me/usage" && call.Bearer);
    }

    [Fact]
    public async Task AvailableAiUsageIsShown()
    {
        using var factory = new BillingUiFactory();
        factory.Backend.AiAvailable = true;
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Billing/Plans"));
        Assert.Contains("1.200", html);
        Assert.Contains("5.000 token", html);
        Assert.Contains("3.800 token", html);
        Assert.DoesNotContain("Chưa tải được dữ liệu AI", html);
    }

    [Fact]
    public async Task UsageFailureKeepsSubscriptionVisible()
    {
        using var factory = new BillingUiFactory();
        factory.Backend.UsageHttpStatus = 503;
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync("/Billing/Plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Đang hoạt động", html);
        Assert.Contains("Chưa thể tải hạn mức sử dụng", html);
        Assert.DoesNotContain("sensitive-test-diagnostic", html);
    }

    [Fact]
    public async Task UsageUnauthorizedKeepsPublicPricingAvailable()
    {
        using var factory = new BillingUiFactory();
        factory.Backend.UsageHttpStatus = 401;
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync("/Billing/Plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Chưa thể tải hạn mức sử dụng", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
}
