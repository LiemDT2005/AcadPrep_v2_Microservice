using System.Net;
using System.Text.Json;
using Xunit;

namespace Billing.UI.Tests;

public class CheckoutUiTests
{
    private const string Page = "/Billing/Checkout";

    [Theory]
    [InlineData("Learner", "/Billing/Plans", "aria-label=\"Upgrade\"", "/Billing/Plans", "/Billing/Orders")]
    [InlineData("Admin", "/Admin/Billing/Orders", "/Admin/Notifications/Create", "/Admin/Billing/Revenue", "/Admin/Ai/Usage")]
    public async Task SignedInRoleHasNavigationToItsServicePages(string role, string page,
        string first, string second, string third)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.Roles = [role];
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(page));
        Assert.Contains(first, html);
        Assert.Contains($"href=\"{second}\"", html);
        Assert.Contains($"href=\"{third}\"", html);
        if (role == "Learner") Assert.DoesNotContain("href=\"/Billing/Plans#my-plan\"", html);
    }

    [Fact]
    public async Task GuestLoginReturnsToCheckoutWithSelectedPlanAndRenewal()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        var checkout = $"{Page}?planId={BillingBackend.MonthId}&renew=true";

        using var guest = await client.GetAsync(checkout);
        Assert.Equal(HttpStatusCode.Redirect, guest.StatusCode);
        var login = guest.Headers.Location!.OriginalString;
        Assert.StartsWith("/Account/Login?", login);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(login));
        Assert.Contains($"action=\"{login}\"", html);

        using var signedIn = await client.PostAsync(login, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = BillingUiFactory.Antiforgery(html),
            ["Email"] = "test@example.test",
            ["Password"] = "test-only-password"
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal(checkout, signedIn.Headers.Location!.OriginalString);
        var returned = await client.GetStringAsync(checkout);
        Assert.Contains("checked=\"checked\"", returned);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Moderator")]
    public async Task OnlyLearnersOpenCheckoutOrResult(string role)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.Roles = [role];
        using var client = factory.Browser();
        await factory.Login(client);
        using var checkout = await client.GetAsync(Page);
        using var result = await client.GetAsync("/Billing/PaymentResult?orderCode=ORD123");
        Assert.Equal(HttpStatusCode.Forbidden, checkout.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task CheckoutListsPaidPlansWithCustomValidationAndPreselection()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        var raw = await client.GetStringAsync($"{Page}?planId={BillingBackend.MonthId}");
        Assert.Contains($"value=\"{BillingBackend.MonthId}\"", raw);
        Assert.DoesNotContain($"value=\"{BillingBackend.FreeId}\"", raw);
        Assert.Contains("data-val-validplan=", raw);
        Assert.Contains($"data-val-validplan-allowed=\"{BillingBackend.MonthId}\"", raw);
        Assert.Contains("checked=\"checked\"", raw);
        Assert.Contains("billing-validation.js", raw);
    }

    [Theory]
    [InlineData(201, false, "/billing/api/orders/checkout")]
    [InlineData(200, false, "/billing/api/orders/checkout")] // reused pending order: same link again
    [InlineData(201, true, "/billing/api/subscriptions/renew")]
    public async Task ValidChoiceRedirectsToTheVnpayLinkFromBilling(int upstream, bool renew, string path)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.CheckoutStatus = upstream;
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(await Form(client, BillingBackend.MonthId, renew)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(factory.Backend.CheckoutPaymentUrl, response.Headers.Location!.ToString());
        var call = Assert.Single(factory.Backend.CheckoutCalls);
        Assert.Equal(path, call.Path);
        using var json = JsonDocument.Parse(call.Body);
        Assert.Equal(new[] { "planId" }, json.RootElement.EnumerateObject().Select(p => p.Name).ToArray()); // never an amount
        // TestServer has no remote IP, so X-Forwarded-For is only sent in a real host (Billing falls back to 127.0.0.1).
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("10000000-0000-0000-0000-000000000001")] // FREE is not on sale for checkout
    [InlineData("99999999-9999-9999-9999-999999999999")]
    public async Task InvalidPlanIsRejectedBeforeCallingBilling(string planId)
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        var form = await Form(client, BillingBackend.MonthId, false);
        form["Input.PlanId"] = planId;
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Vui lòng chọn một gói trả phí đang bán", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Empty(factory.Backend.CheckoutCalls);
    }

    [Theory]
    [InlineData(422, "NoSubscriptionToRenew", "Bạn chưa có gói nào để gia hạn")]
    [InlineData(422, "PlanNotPurchasable", "Gói này hiện không thể thanh toán")]
    [InlineData(503, null, "Cổng thanh toán đang tạm thời không khả dụng")]
    public async Task BillingErrorsAreFriendlyWithoutDiagnostics(int upstream, string? code, string expected)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.CheckoutStatus = upstream;
        factory.Backend.CheckoutCode = code;
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(await Form(client, BillingBackend.MonthId, true)));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(expected, html);
        Assert.DoesNotContain("sensitive-test-diagnostic", html);
    }

    [Fact]
    public async Task NonVnpayPaymentLinkIsNeverFollowed()
    {
        using var factory = new BillingUiFactory();
        factory.Backend.CheckoutPaymentUrl = "https://evil.example/pay";
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(await Form(client, BillingBackend.MonthId, false)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Không thể mở cổng thanh toán", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("", "Thanh toán thành công")]
    [InlineData("&error=invalid_signature", "Không xác minh được phản hồi từ VNPay")]
    public async Task ResultPageShowsTheStatusFromTheApiNotTheUrl(string extra, string expected)
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Billing/PaymentResult?orderCode=ORD123{extra}&vnp_ResponseCode=24"));
        Assert.Contains(expected, html);
        Assert.Contains("data-status=\"Paid\"", html); // from GET /billing/api/orders/ORD123
        Assert.Contains(factory.Backend.Calls, call => call.Path == "/billing/api/orders/ORD123" && call.Bearer);
    }

    [Fact]
    public async Task PricingPageOffersDirectRenewal()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        var raw = await client.GetStringAsync("/Billing/Plans");
        Assert.Contains("action=\"/Billing/Plans?handler=Pay\"", raw);
    }

    [Theory]
    [InlineData("Free", "/billing/api/orders/checkout")]
    [InlineData("Active", "/billing/api/subscriptions/renew")]
    public async Task PricingPaymentGoesStraightToVnpay(string subscription, string path)
    {
        using var factory = new BillingUiFactory();
        factory.Backend.SubscriptionStatus = subscription;
        using var client = factory.Browser();
        await factory.Login(client);
        var html = await client.GetStringAsync("/Billing/Plans");
        using var response = await client.PostAsync("/Billing/Plans?handler=Pay", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = BillingUiFactory.Antiforgery(html),
            ["planId"] = BillingBackend.MonthId.ToString()
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(factory.Backend.CheckoutPaymentUrl, response.Headers.Location!.OriginalString);
        Assert.Equal(path, Assert.Single(factory.Backend.CheckoutCalls).Path);
    }

    private static async Task<Dictionary<string, string>> Form(HttpClient client, Guid planId, bool renew)
    {
        var html = await client.GetStringAsync($"{Page}?planId={planId}&renew={renew.ToString().ToLowerInvariant()}");
        return new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = BillingUiFactory.Antiforgery(html),
            ["Input.PlanId"] = planId.ToString(), ["Input.Renew"] = renew.ToString().ToLowerInvariant()
        };
    }
}
