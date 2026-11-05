using System.Net;
using System.Text.Json;
using BuildingBlocks.Common.Events;
using Billing.API.DTOs;
using Billing.API.Entities;
using Billing.API.Services;
using Billing.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static Billing.API.Tests.BillingTestData;
using static Billing.API.Tests.PaymentTestHelpers;

namespace Billing.API.Tests;

public class PaymentConfirmationTests
{
    [Fact]
    public async Task SuccessfulIpnPaysActivatesQueuesEventInvalidatesRevenueAndIsIdempotent()
    {
        var cache = new SpyCache();
        using var factory = Factory(cache);
        var (user, code) = await NewOrder(factory);
        Assert.Equal(1, cache.Invalidations); // new Pending order changes the status counts
        using var anonymous = factory.Client(null);
        Assert.Equal(("00", "Confirm Success"), await Ipn(anonymous, Callback(factory, code, 11)));
        await factory.WithDb(async db =>
        {
            var order = await db.Orders.SingleAsync();
            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.NotNull(order.PaidAt);
            Assert.Equal("14226112", order.VnpTransactionNo);
            var subscription = await db.Subscriptions.SingleAsync();
            Assert.Equal((user, order.Id, SubscriptionStatus.Active), (subscription.UserId, subscription.SourceOrderId, subscription.Status));
            Assert.Equal(subscription.StartDate.AddDays(30), subscription.EndDate);
            var message = await db.OutboxMessages.SingleAsync();
            Assert.Equal(nameof(SubscriptionActivatedEvent), message.Type);
            var @event = JsonSerializer.Deserialize<SubscriptionActivatedEvent>(message.Payload)!;
            Assert.Equal((code, false, "Test month"), (@event.OrderCode, @event.IsRenewal, @event.PlanName));
        });
        Assert.Equal(2, cache.Invalidations);

        // VNPay retries / duplicate delivery: already confirmed, nothing extended twice.
        Assert.Equal(("02", "Order already confirmed"), await Ipn(anonymous, Callback(factory, code, 11)));
        await factory.WithDb(async db =>
        {
            Assert.Equal(1, await db.Subscriptions.CountAsync());
            Assert.Equal(1, await db.OutboxMessages.CountAsync());
            Assert.Equal(3, await db.PaymentLogs.CountAsync()); // Create + 2 IPN
        });
        Assert.Equal(2, cache.Invalidations);
    }

    [Fact]
    public async Task PaymentForAnActiveLearnerExtendsFromTheCurrentEndDate()
    {
        using var factory = Factory();
        var user = Guid.NewGuid();
        var end = factory.Clock.Now.UtcDateTime.AddDays(10);
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(Subscription(user, end.AddDays(-20), end));
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner", user);
        var code = (await Checkout(client, QuarterId, HttpStatusCode.Created, "/api/subscriptions/renew"))
            .GetProperty("orderCode").GetString()!;
        Assert.Equal("00", (await Ipn(factory.Client(null), Callback(factory, code, 22))).RspCode);
        await factory.WithDb(async db =>
        {
            var subscription = await db.Subscriptions.SingleAsync();
            Assert.Equal(end.AddDays(90), subscription.EndDate, TimeSpan.FromMilliseconds(1));
            Assert.Equal(QuarterId, subscription.PlanId);
            var @event = JsonSerializer.Deserialize<SubscriptionActivatedEvent>((await db.OutboxMessages.SingleAsync()).Payload)!;
            Assert.True(@event.IsRenewal);
        });
    }

    [Theory]
    [InlineData("amount", "04")]
    [InlineData("no-amount", "04")]
    [InlineData("unknown-order", "01")]
    [InlineData("bad-signature", "97")]
    [InlineData("other-merchant", "97")]
    public async Task InvalidCallbacksNeverChangeTheOrder(string mode, string expected)
    {
        using var factory = Factory();
        var (_, code) = await NewOrder(factory);
        var callback = mode switch
        {
            "amount" => Callback(factory, code, 10),
            "no-amount" => Callback(factory, code, 11, includeAmount: false),
            "unknown-order" => Callback(factory, "99999999999999", 11),
            "other-merchant" => Callback(factory, code, 11, tmnCode: "OTHERMER"),
            _ => Callback(factory, code, 11)
        };
        if (mode == "bad-signature") callback["vnp_ResponseCode"] = "00 ";
        Assert.Equal(expected, (await Ipn(factory.Client(null), callback)).RspCode);
        await factory.WithDb(async db =>
        {
            Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status);
            Assert.Equal(0, await db.Subscriptions.CountAsync());
            Assert.Equal(expected, (await db.PaymentLogs.OrderByDescending(l => l.CreatedAt).FirstAsync(l => l.Type == PaymentLogType.Ipn)).RspCode);
        });
    }

    [Theory]
    [InlineData("24", "02")]
    [InlineData("00", "02")] // ResponseCode 00 but TransactionStatus not 00 is not a payment
    public async Task UnsuccessfulPaymentFailsTheOrderWithoutActivation(string responseCode, string transactionStatus)
    {
        var cache = new SpyCache();
        using var factory = Factory(cache);
        var (_, code) = await NewOrder(factory);
        using var anonymous = factory.Client(null);
        Assert.Equal("00", (await Ipn(anonymous, Callback(factory, code, 11, responseCode, transactionStatus))).RspCode);
        await factory.WithDb(async db =>
        {
            Assert.Equal(OrderStatus.Failed, (await db.Orders.SingleAsync()).Status);
            Assert.Equal(0, await db.Subscriptions.CountAsync());
        });
        // The Failed count on the revenue page must not be served from a stale cache entry.
        Assert.Equal(2, cache.Invalidations);
        // A later "success" for the same failed order is not applied.
        Assert.Equal("02", (await Ipn(anonymous, Callback(factory, code, 11))).RspCode);
        await factory.WithDb(async db => Assert.Equal(0, await db.Subscriptions.CountAsync()));
    }

    [Fact]
    public async Task LateSuccessForAnOrderExpiredByTheJobIsStillHonoured()
    {
        var cache = new SpyCache();
        using var factory = Factory(cache);
        var (_, code) = await NewOrder(factory);
        factory.Clock.Now = factory.Clock.Now.AddMinutes(20);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<IOrderExpiryService>().RunOnceAsync(default));
        Assert.Equal(2, cache.Invalidations); // checkout + expiry job
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IOrderExpiryService>().RunOnceAsync(default));
        Assert.Equal(2, cache.Invalidations); // nothing expired: cache kept
        await factory.WithDb(async db => Assert.Equal(OrderStatus.Expired, (await db.Orders.SingleAsync()).Status));
        Assert.Equal("00", (await Ipn(factory.Client(null), Callback(factory, code, 11))).RspCode);
        await factory.WithDb(async db =>
        {
            Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync()).Status);
            Assert.Equal(1, await db.Subscriptions.CountAsync());
        });
    }

    [Fact]
    public async Task SignedReturnConfirmsOnLocalhostAndTheLaterIpnIsAlreadyConfirmed()
    {
        using var factory = Factory();
        var (_, code) = await NewOrder(factory);
        using var browser = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await browser.GetAsync(Url("/api/payments/vnpay-return", Callback(factory, code, 11)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"http://storefront.test/Billing/PaymentResult?orderCode={code}", response.Headers.Location!.ToString());
        await factory.WithDb(async db => Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync()).Status));
        Assert.Equal("02", (await Ipn(factory.Client(null), Callback(factory, code, 11))).RspCode);
        await factory.WithDb(async db => Assert.Equal(1, await db.Subscriptions.CountAsync()));
    }

    [Fact]
    public async Task TamperedReturnRedirectsWithAnErrorAndChangesNothing()
    {
        using var factory = Factory();
        var (_, code) = await NewOrder(factory);
        var callback = Callback(factory, code, 11);
        callback["vnp_Amount"] = "1"; // tampered after signing
        using var browser = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await browser.GetAsync(Url("/api/payments/vnpay-return", callback));
        Assert.Equal("http://storefront.test/Billing/PaymentResult?error=invalid_signature", response.Headers.Location!.ToString());
        await factory.WithDb(async db => Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync()).Status));
    }

    [Fact]
    public async Task IpnAcknowledgementUsesVnpayPropertyNames()
    {
        using var factory = Factory();
        await factory.WithDb(db => SeedPlans(db));
        using var response = await factory.Client(null).GetAsync("/api/payments/vnpay-ipn?vnp_TxnRef=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"RspCode\":\"97\",\"Message\":\"Invalid signature\"}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaleOrderVersionCannotConfirmTwice()
    {
        using var factory = Factory();
        var (_, code) = await NewOrder(factory);
        using var scopeA = factory.Services.CreateScope();
        var stale = scopeA.ServiceProvider.GetRequiredService<Billing.API.Data.BillingDbContext>();
        var order = await stale.Orders.SingleAsync();
        Assert.Equal("00", (await Ipn(factory.Client(null), Callback(factory, code, 11))).RspCode);
        order.Status = OrderStatus.Failed; order.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    private static BillingApiFactory Factory(SpyCache? cache = null) => new()
    {
        ExtraServices = cache is null ? null : services =>
        {
            services.RemoveAll<IRevenueCache>();
            services.AddSingleton<IRevenueCache>(cache);
        }
    };

    private static async Task<(Guid User, string Code)> NewOrder(BillingApiFactory factory)
    {
        await factory.WithDb(db => SeedPlans(db));
        var user = Guid.NewGuid();
        using var client = factory.Client("Learner", user);
        return (user, (await Checkout(client, MonthId, HttpStatusCode.Created)).GetProperty("orderCode").GetString()!);
    }

    private sealed class SpyCache : IRevenueCache
    {
        public int Invalidations { get; private set; }
        public Task<RevenueDto?> GetAsync(string key, CancellationToken ct) => Task.FromResult<RevenueDto?>(null);
        public Task SetAsync(string key, RevenueDto value, CancellationToken ct) => Task.CompletedTask;
        public Task InvalidateAsync(CancellationToken ct) { Invalidations++; return Task.CompletedTask; }
    }
}
