using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Billing.API.Entities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Billing.API.Tests.BillingTestData;
using static Billing.API.Tests.PaymentTestHelpers;

namespace Billing.API.Tests;

public class CheckoutTests
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Admin", 403)]
    [InlineData("Moderator", 403)]
    public async Task OnlyLearnersCheckoutOrRenew(string? role, int expected)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client(role);
        using var checkout = await client.PostAsJsonAsync("/api/orders/checkout", new { planId = MonthId });
        using var renew = await client.PostAsJsonAsync("/api/subscriptions/renew", new { planId = MonthId });
        Assert.Equal(expected, (int)checkout.StatusCode);
        Assert.Equal(expected, (int)renew.StatusCode);
    }

    [Fact]
    public async Task CheckoutCreatesASignedVnpayLinkFromServerPricesAndSnapshotsTheOrder()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        var user = Guid.NewGuid();
        using var client = factory.Client("Learner", user);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7, 10.0.0.1");
        var body = await Checkout(client, MonthId, HttpStatusCode.Created);
        Assert.False(body.GetProperty("reused").GetBoolean());
        var code = body.GetProperty("orderCode").GetString()!;
        Assert.Matches("^[1-9][0-9]{13}$", code);

        var url = new Uri(body.GetProperty("paymentUrl").GetString()!);
        Assert.Equal("https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", url.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(url.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
        Assert.Equal("1100", query["vnp_Amount"]);            // test plan price 11 VND x 100
        Assert.Equal(code, query["vnp_TxnRef"]);
        Assert.Equal("TESTTMN1", query["vnp_TmnCode"]);
        Assert.Equal("2.1.0", query["vnp_Version"]);
        Assert.Equal("pay", query["vnp_Command"]);
        Assert.Equal("VND", query["vnp_CurrCode"]);
        Assert.Equal("vn", query["vnp_Locale"]);
        Assert.Equal("other", query["vnp_OrderType"]);
        Assert.Equal("203.0.113.7", query["vnp_IpAddr"]);
        Assert.Equal($"Thanh toan goi PRO_MONTH don {code}", query["vnp_OrderInfo"]);
        Assert.Equal("http://gateway.test/billing/api/payments/vnpay-return", query["vnp_ReturnUrl"]);
        var created = DateTime.ParseExact(query["vnp_CreateDate"], "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var expires = DateTime.ParseExact(query["vnp_ExpireDate"], "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        Assert.Equal(TimeSpan.FromMinutes(15), expires - created);
        Assert.Equal(factory.Clock.Now.UtcDateTime.AddHours(7), created, TimeSpan.FromSeconds(1)); // GMT+7
        Assert.False(query.ContainsKey("vnp_BankCode"));
        Assert.Equal(Sign(factory.VnpayHashSecret, query), query["vnp_SecureHash"]);
        Assert.DoesNotContain(factory.VnpayHashSecret, url.ToString());

        await factory.WithDb(async db =>
        {
            var order = await db.Orders.SingleAsync();
            Assert.Equal((user, "PRO_MONTH", "Test month", 30, 11m, OrderKind.Purchase, OrderStatus.Pending),
                (order.UserId, order.PlanCode, order.PlanName, order.DurationDays, order.Amount, order.Kind, order.Status));
            Assert.Equal(url.ToString(), new Uri(order.PaymentUrl!).ToString());
            var log = await db.PaymentLogs.SingleAsync();
            Assert.Equal(PaymentLogType.Create, log.Type);
            Assert.Equal(order.Id, log.OrderId);
        });
    }

    [Fact]
    public async Task OnePendingOrderPerPlanIsReusedEvenAcrossPurchaseAndRenewal()
    {
        using var factory = new BillingApiFactory();
        var user = Guid.NewGuid();
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(Subscription(user, DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10),
                SubscriptionStatus.Expired));
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner", user);
        var month = await Checkout(client, MonthId, HttpStatusCode.Created);
        var quarter = await Checkout(client, QuarterId, HttpStatusCode.Created);
        // Third, fourth click on either plan: back to the order that is already pending, same link.
        var monthAgain = await Checkout(client, MonthId, HttpStatusCode.OK);
        var quarterRenew = await Checkout(client, QuarterId, HttpStatusCode.OK, "/api/subscriptions/renew");
        Assert.True(monthAgain.GetProperty("reused").GetBoolean());
        Assert.Equal(month.GetProperty("orderCode").GetString(), monthAgain.GetProperty("orderCode").GetString());
        Assert.Equal(month.GetProperty("paymentUrl").GetString(), monthAgain.GetProperty("paymentUrl").GetString());
        Assert.Equal(quarter.GetProperty("orderCode").GetString(), quarterRenew.GetProperty("orderCode").GetString());
        await factory.WithDb(async db => Assert.Equal(2, await db.Orders.CountAsync()));
    }

    [Fact]
    public async Task AnExpiredPendingOrderIsClosedAndReplaced()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client("Learner");
        var first = await Checkout(client, MonthId, HttpStatusCode.Created);
        factory.Clock.Now = factory.Clock.Now.AddMinutes(16);
        var second = await Checkout(client, MonthId, HttpStatusCode.Created);
        Assert.NotEqual(first.GetProperty("orderCode").GetString(), second.GetProperty("orderCode").GetString());
        await factory.WithDb(async db =>
        {
            Assert.Equal(OrderStatus.Expired,
                (await db.Orders.SingleAsync(o => o.OrderCode == first.GetProperty("orderCode").GetString())).Status);
            Assert.Equal(1, await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending));
        });
    }

    [Fact]
    public async Task DatabaseRejectsASecondPendingOrderForTheSamePlan()
    {
        using var factory = new BillingApiFactory();
        var user = Guid.NewGuid();
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Orders.Add(OrderTestData.Order(user, "PEND1", DateTime.UtcNow, OrderStatus.Pending));
            await db.SaveChangesAsync();
            db.Orders.Add(OrderTestData.Order(user, "PEND2", DateTime.UtcNow, OrderStatus.Pending));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Theory]
    [InlineData("free", "/api/orders/checkout", 422, "PlanNotPurchasable")]
    [InlineData("inactive", "/api/orders/checkout", 404, null)]
    [InlineData("missing", "/api/orders/checkout", 404, null)]
    [InlineData("month", "/api/subscriptions/renew", 422, "NoSubscriptionToRenew")]
    public async Task InvalidPlanOrRenewalWithoutHistoryIsRejected(string plan, string path, int expected, string? code)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db, quarterActive: false));
        var planId = plan switch { "free" => FreeId, "inactive" => QuarterId, "month" => MonthId, _ => Guid.NewGuid() };
        using var client = factory.Client("Learner");
        using var response = await client.PostAsJsonAsync(path, new { planId });
        Assert.Equal(expected, (int)response.StatusCode);
        if (code is not null) Assert.Contains(code, await response.Content.ReadAsStringAsync());
        await factory.WithDb(async db => Assert.Equal(0, await db.Orders.CountAsync()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"planId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"planId\":\"10000000-0000-0000-0000-000000000002\",\"amount\":1}")]
    public async Task InvalidBodyIs400AndClientAmountIsNeverAccepted(string json)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client("Learner");
        using var response = await client.PostAsync("/api/orders/checkout",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingVnpayCredentialsMakeCheckoutUnavailable()
    {
        using var factory = new BillingApiFactory { VnpayTmnCode = "", VnpayHashSecret = "" };
        await factory.WithDb(db => SeedPlans(db));
        using var client = factory.Client("Learner");
        using var response = await client.PostAsJsonAsync("/api/orders/checkout", new { planId = MonthId });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
