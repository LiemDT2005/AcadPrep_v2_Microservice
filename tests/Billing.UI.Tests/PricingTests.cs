using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Web.Models;
using Storefront.Web.Services;
using Xunit;

namespace Billing.UI.Tests;

public class PricingTests
{
    [Fact]
    public async Task GuestCanOpenPricingFromUpgradeAndOnlyViewPlans()
    {
        using var factory = new PricingFactory();
        using var client = factory.CreateClient();
        var home = await client.GetStringAsync("/");
        Assert.Matches("<a[^>]+href=\"/Billing/Plans\"[^>]*>Upgrade</a>", home);
        Assert.DoesNotContain("href=\"/Billing/Orders\"", home);
        using var response = await client.GetAsync("/Billing/Plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("79.000", html);
        Assert.DoesNotContain("Đăng nhập để mua", html);
        Assert.DoesNotContain("Bài thi đầy đủ", html);
        Assert.DoesNotContain("Không giới hạn", html);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;", raw);
        Assert.DoesNotContain("<script>alert(1)</script>", raw);
        Assert.DoesNotContain("handler=Pay", raw);
        Assert.DoesNotContain("href=\"/Billing/Checkout", raw);
        Assert.Equal("/billing/api/plans", factory.Backend.Path);
    }

    [Fact]
    public async Task EmptyPricingIsNotBackendFailure()
    {
        using var factory = new PricingFactory();
        factory.Backend.Empty = true;
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/Billing/Plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("chưa có gói học", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData(503, false, 503)]
    [InlineData(500, false, 502)]
    [InlineData(200, true, 502)]
    public async Task FailureDoesNotBecomeAnEmptyOrFreePlan(int upstream, bool badJson, int expected)
    {
        using var factory = new PricingFactory();
        factory.Backend.Status = upstream;
        factory.Backend.BadJson = badJson;
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/Billing/Plans");
        Assert.Equal(expected, (int)response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Chưa thể tải bảng giá", html);
        Assert.DoesNotContain("Hiện chưa có gói học", html);
        Assert.DoesNotContain("sensitive-test-diagnostic", html);
    }
}

internal class PricingFactory : WebApplicationFactory<BillingApiClient>
{
    public PricingBackend Backend { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services => services.AddHttpClient<BillingApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => Backend));
    }
}

internal class PricingBackend : HttpMessageHandler
{
    public int Status { get; set; } = 200;
    public bool Empty { get; set; }
    public bool BadJson { get; set; }
    public string? Path { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Path = request.RequestUri!.AbsolutePath;
        if (Path != "/billing/api/plans") throw new InvalidOperationException("Unexpected test route.");
        var response = new HttpResponseMessage((HttpStatusCode)Status);
        response.Content = BadJson || Status != 200 ? new StringContent("sensitive-test-diagnostic")
            : JsonContent.Create(Empty ? Array.Empty<BillingPlan>() : new[] {
                new BillingPlan(Guid.NewGuid(), "PRO_MONTH", "Test <script>alert(1)</script>", "Test-only fixture",
                    79000, 30, [new BillingPlanLimit("FullTest", null, "Monthly")]) });
        return Task.FromResult(response);
    }
}
