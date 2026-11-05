using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Web.Models;
using Storefront.Web.Services;
using Xunit;

namespace Billing.UI.Tests;

public class SubscriptionUiTests
{
    [Fact]
    public async Task WithoutSessionRedirectsToLoginWithoutCallingBilling()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        using var response = await client.GetAsync("/Billing/Subscription");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
        Assert.DoesNotContain(factory.Backend.Calls, call => call.Path.StartsWith("/billing/"));
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Free", false)]
    public async Task PricingMarksTheSignedInLearnersCurrentPlan(string status, bool pro)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.SubscriptionStatus = status;
        using var client = factory.Browser();
        await factory.Login(client);
        var raw = await client.GetStringAsync("/Billing/Plans");
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("Gói hiện tại", html);
        Assert.Equal(pro, html.Contains("Gói hiện tại · còn 12 ngày"));
        Assert.Contains("handler=Pay", raw);
        Assert.Equal(pro, html.Contains("Gia hạn thêm 30 ngày"));
        Assert.Equal(pro, raw.Contains("class=\"storefront-pro-badge\">PRO</span>"));
        Assert.Single(factory.Backend.Calls, call => call.Path == "/billing/api/subscriptions/me");
    }

    [Fact]
    public async Task PricingForGuestsAndStaffHasNoCurrentPlanMarker()
    {
        using var factory = new BillingUiFactory();
        var guest = WebUtility.HtmlDecode(await factory.Browser().GetStringAsync("/Billing/Plans"));
        Assert.DoesNotContain("Gói hiện tại", guest);
        Assert.DoesNotContain("storefront-pro-badge", guest);
        factory.Backend.Roles = ["Admin"];
        factory.Backend.SubscriptionHttpStatus = 403; // Billing is Learner-only
        using var admin = factory.Browser();
        await factory.Login(admin);
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/Billing/Plans"));
        Assert.DoesNotContain("Gói hiện tại", html);
        Assert.DoesNotContain("handler=Pay", html);
        Assert.DoesNotContain("storefront-pro-badge", html);
        Assert.DoesNotContain(factory.Backend.Calls, call => call.Path == "/billing/api/subscriptions/me");
    }

    [Fact]
    public async Task ActiveSubscriptionRendersApiDataEncodedWithBearer()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync("/Billing/Plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("Đang hoạt động", html);
        Assert.Contains("Còn lại: <strong>12 ngày</strong>", html);
        Assert.Contains("01/12/2026 07:00", html); // UTC midnight shown in UTC+7
        Assert.Contains("&lt;b&gt;", raw);
        Assert.DoesNotContain("<b>Pro</b>", raw);
        Assert.Contains(factory.Backend.Calls, call => call.Path == "/billing/api/subscriptions/me" && call.Bearer);
    }

    [Theory]
    [InlineData("Expired", "Đã hết hạn")]
    [InlineData("Free", "Bạn chưa đăng ký gói trả phí")]
    public async Task NonActiveStatesAreDistinct(string status, string expected)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.SubscriptionStatus = status;
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Billing/Plans"));
        Assert.Contains(expected, html);
        Assert.DoesNotContain("Đang hoạt động", html);
        Assert.DoesNotContain("storefront-pro-badge", html);
    }

    [Fact]
    public async Task ActiveLearnerSeesProBadgeOutsidePricing()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);

        var html = await client.GetStringAsync("/Billing/Orders");

        Assert.Contains("class=\"storefront-pro-badge\">PRO</span>", html);
        Assert.Contains(factory.Backend.Calls, call => call.Path == "/billing/api/subscriptions/me" && call.Bearer);
    }

    [Theory]
    [InlineData(503)]
    [InlineData(500)]
    public async Task FailureIsNotShownAsFree(int upstream)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.SubscriptionHttpStatus = upstream;
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync("/Billing/Plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Chưa thể tải thông tin gói", html);
        Assert.DoesNotContain("storefront-pro-badge", html);
        Assert.DoesNotContain("Gói hiện tại:", html);
        Assert.DoesNotContain("sensitive-test-diagnostic", html);
    }

    [Fact]
    public async Task ExpiredSessionAtBillingLeavesPublicPricingAvailable()
    {
        using var factory = new BillingUiFactory();
        factory.Backend.SubscriptionHttpStatus = 401;
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync("/Billing/Plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Chưa thể tải thông tin gói", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
}

internal class BillingUiFactory : WebApplicationFactory<BillingApiClient>
{
    public BillingBackend Backend { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient<ToeicApiClient>().ConfigurePrimaryHttpMessageHandler(() => Backend);
            services.AddHttpClient<BillingApiClient>().ConfigurePrimaryHttpMessageHandler(() => Backend);
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    public async Task Login(HttpClient client)
    {
        var token = Antiforgery(await client.GetStringAsync("/Account/Login"));
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Email"] = "test@example.test", ["Password"] = "test-only-password"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public static string Antiforgery(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
}

// Test-only upstream simulator for Identity and Billing; production uses the Gateway.
internal class BillingBackend : HttpMessageHandler
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid MonthId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid FreeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public string[] Roles { get; set; } = ["Learner"];
    public string SubscriptionStatus { get; set; } = "Active";
    public int SubscriptionHttpStatus { get; set; } = 200;
    public int PlansStatus { get; set; } = 200;
    public int UsageHttpStatus { get; set; } = 200;
    public bool AiAvailable { get; set; }
    public int OrdersHttpStatus { get; set; } = 200;
    public bool EmptyOrders { get; set; }
    public int CsvStatus { get; set; } = 200;
    public ConcurrentBag<string> AdminOrderQueries { get; } = new();
    public int CheckoutStatus { get; set; } = 201;
    public string? CheckoutCode { get; set; }
    public string CheckoutPaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_TxnRef=12345678901234&vnp_SecureHash=abc";
    public ConcurrentBag<(string Path, string Body, string? ForwardedFor)> CheckoutCalls { get; } = new();
    public int RevenueStatus { get; set; } = 200;
    public bool RevenueZero { get; set; }
    public ConcurrentBag<string> RevenueQueries { get; } = new();
    public int GrantStatus { get; set; } = 201;
    public ConcurrentQueue<(string Method, string Path, bool Bearer)> Calls { get; } = new();
    public List<string> GrantBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.PathAndQuery;
        Calls.Enqueue((request.Method.Method, path, request.Headers.Authorization?.Scheme == "Bearer" &&
            request.Headers.Authorization.Parameter == "test-session-token"));
        if (path == "/identity/api/auth/login") return Json(200, new AuthResponse("test-session-token", "test@example.test",
            "Test User", UserId.ToString(), DateTime.UtcNow.AddHours(1), Roles.ToList()));
        if (path == "/identity/api/auth/me") return Json(200, new UserProfileDto(UserId.ToString(),
            "test@example.test", "Test User", null, "Active", Roles.ToList(), DateTime.UtcNow, null));
        // The shared layout renders the existing notification badge.
        if (path == "/notification/api/notifications/unread-count") return Json(200, new NotificationUnreadCount(0));
        if (path.StartsWith("/billing/api/orders/me"))
        {
            if (OrdersHttpStatus != 200) return Text(OrdersHttpStatus, "sensitive-test-diagnostic");
            return Json(200, new BillingPage<BillingOrderItem>(EmptyOrders ? [] : [
                new BillingOrderItem("ORD123", "<i>Pro</i>", 79000, "Purchase", "Expired",
                    new DateTimeOffset(2026, 10, 18, 17, 30, 0, TimeSpan.Zero), null)], 1, 20, EmptyOrders ? 0 : 1, EmptyOrders ? 0 : 1));
        }
        if (request.Method == HttpMethod.Get && path.StartsWith("/billing/api/orders/"))
            return path.EndsWith("/ORD123")
                ? Json(200, new BillingOrderDetail("ORD123", "Pro", 79000, "Purchase", "Paid",
                    new DateTimeOffset(2026, 10, 18, 17, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 18, 17, 31, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 10, 18, 17, 45, 0, TimeSpan.Zero)))
                : Json(404, new { title = "Order not found." });
        if (request.Method == HttpMethod.Post && (path == "/billing/api/orders/checkout" || path == "/billing/api/subscriptions/renew"))
        {
            CheckoutCalls.Add((path, await request.Content!.ReadAsStringAsync(ct),
                request.Headers.TryGetValues("X-Forwarded-For", out var forwarded) ? string.Join(",", forwarded) : null));
            return CheckoutStatus is 200 or 201
                ? Json(CheckoutStatus, new BillingCheckoutResult("12345678901234", CheckoutPaymentUrl,
                    DateTimeOffset.UtcNow.AddMinutes(15), CheckoutStatus == 200))
                : Json(CheckoutStatus, new { title = CheckoutCode ?? "error", detail = "sensitive-test-diagnostic" });
        }
        if (path.StartsWith("/billing/api/admin/revenue"))
        {
            RevenueQueries.Add(path);
            if (RevenueStatus != 200) return Text(RevenueStatus, "sensitive-test-diagnostic");
            if (request.Headers.Accept.Any(value => value.MediaType == "text/csv"))
            {
                var csv = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("period,revenue,paidCount\r\n"u8.ToArray()) };
                csv.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
                csv.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                    { FileName = "revenue-day-2026-10-01-2026-10-30.csv" };
                return csv;
            }
            return Json(200, new BillingRevenue(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 30), "day",
                new BillingRevenueTotals(RevenueZero ? 0 : 1_250_000, 3, 1, 2, 4),
                [new BillingRevenuePoint("2026-10-01", RevenueZero ? 0 : 1_250_000, 3), new BillingRevenuePoint("<b>x</b>", 0, 0)]));
        }
        if (path.StartsWith("/billing/api/admin/orders"))
        {
            AdminOrderQueries.Add(path);
            if (request.Headers.Accept.Any(value => value.MediaType == "text/csv"))
            {
                if (CsvStatus != 200) return Text(CsvStatus, "sensitive-test-diagnostic");
                var csv = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("orderCode\r\nA1\r\n"u8.ToArray()) };
                csv.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
                csv.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                    { FileName = "orders-2026-10.csv" };
                return csv;
            }
            return Json(200, new BillingPage<BillingAdminOrder>([
                new BillingAdminOrder("ADM1", UserId, "<b>x</b>@example.test", "PRO_MONTH", 79000, "Purchase", "Paid",
                    new DateTimeOffset(2026, 10, 18, 17, 30, 0, TimeSpan.Zero), null)], 1, 20, 41, 3));
        }
        if (path == "/billing/api/subscriptions/me/usage")
        {
            if (UsageHttpStatus != 200) return Text(UsageHttpStatus, "sensitive-test-diagnostic");
            return Json(200, new BillingUsage("FREE", [
                new BillingFeatureUsage("FullTest", 3, null, null, false, "Monthly", new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero)),
                new BillingFeatureUsage("Practice", null, null, null, false, "Daily", new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero)),
                new BillingFeatureUsage("Vocab", 100, null, null, false, "Lifetime", null)
            ], AiAvailable
                ? new BillingAiUsage(true, 5000, 1200, 3800, new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero))
                : new BillingAiUsage(false, null, null, null, null)));
        }
        if (path == "/billing/api/subscriptions/me")
        {
            if (SubscriptionHttpStatus != 200) return Text(SubscriptionHttpStatus, "sensitive-test-diagnostic");
            return Json(200, SubscriptionStatus switch
            {
                "Active" => new BillingSubscription("Active", "PRO_MONTH", "<b>Pro</b>", true,
                    new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), 12),
                "Expired" => new BillingSubscription("Expired", "FREE", "Free", false, null,
                    new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), 0),
                _ => new BillingSubscription("Free", "FREE", "Free", false, null, null, null)
            });
        }
        if (path == "/billing/api/plans")
            return PlansStatus != 200 ? Text(PlansStatus, "sensitive-test-diagnostic") : Json(200, new[]
            {
                new BillingPlan(FreeId, "FREE", "Test free", null, 0, 0, []),
                new BillingPlan(MonthId, "PRO_MONTH", "Test month", null, 11, 30, [])
            });
        if (request.Method == HttpMethod.Post && path == "/billing/api/admin/subscriptions/grant")
        {
            GrantBodies.Add(await request.Content!.ReadAsStringAsync(ct));
            return GrantStatus == 201
                ? Json(201, new BillingGrantResult(Guid.NewGuid(), DateTimeOffset.UtcNow, new DateTimeOffset(2026, 12, 5, 0, 0, 0, TimeSpan.Zero)))
                : Json(GrantStatus, new { errors = new { userId = new[] { "internal-diagnostic-must-not-leak" } } });
        }
        throw new InvalidOperationException("Unexpected upstream route in UI test.");
    }

    private static HttpResponseMessage Json(int status, object data) => new((HttpStatusCode)status)
        { Content = JsonContent.Create(data) };
    private static HttpResponseMessage Text(int status, string text) => new((HttpStatusCode)status)
        { Content = new StringContent(text) };
    protected override void Dispose(bool disposing) { }
}
