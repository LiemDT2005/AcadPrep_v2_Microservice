using System.Net;
using Billing.API.Services;
using Billing.API.Entities;
using Billing.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Billing.API.Tests.OrderTestData;
using static Billing.API.Tests.OrderTests;

namespace Billing.API.Tests;

public class RevenueCacheTests
{
    private const string Url = "/api/admin/revenue?from=2026-10-18&to=2026-10-20";

    [Fact]
    public async Task SecondReadComesFromCacheUntilInvalidated()
    {
        var storage = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        using var factory = WithCache(storage);
        await RevenueTests.Seeded(factory);
        using var client = factory.Client("Admin");
        var first = await Json(client, Url);

        // A new Paid order is not visible while the cached entry is valid (TTL 1 hour)...
        await factory.WithDb(async db =>
        {
            var order = Order(Guid.NewGuid(), "NEWPAID", new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc), amount: 50);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        });
        var cached = await Json(client, Url);
        Assert.Equal(first.GetProperty("totals").GetProperty("revenue").GetDecimal(),
            cached.GetProperty("totals").GetProperty("revenue").GetDecimal());

        // ...and becomes visible immediately after the IPN-triggered invalidation (VNPay task will call this).
        await factory.Services.GetRequiredService<IRevenueCache>().InvalidateAsync(default);
        var fresh = await Json(client, Url);
        Assert.Equal(first.GetProperty("totals").GetProperty("revenue").GetDecimal() + 50,
            fresh.GetProperty("totals").GetProperty("revenue").GetDecimal());
    }

    [Fact]
    public async Task RedisFailureFallsBackToTheDatabaseWith200()
    {
        using var factory = WithCache(new BrokenCache());
        await RevenueTests.Seeded(factory);
        using var client = factory.Client("Admin");
        using var response = await client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(300m, (await Json(client, Url)).GetProperty("totals").GetProperty("revenue").GetDecimal());
        await factory.Services.GetRequiredService<IRevenueCache>().InvalidateAsync(default); // never throws
    }

    [Fact]
    public async Task CachedRevenueNeverHidesAFailedOrderFromTheStatusCounts()
    {
        using var factory = WithCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        await RevenueTests.Seeded(factory);
        using var client = factory.Client("Admin");
        var before = await Json(client, Url);
        await factory.WithDb(async db =>
        {
            var pending = await db.Orders.FirstAsync(order => order.Status == OrderStatus.Pending && order.ExpiresAt > factory.Clock.Now.UtcDateTime);
            pending.Status = OrderStatus.Failed;
            await db.SaveChangesAsync();
        });
        // Simulate an existing Redis entry left by an earlier deployment: no invalidation is issued here.
        var after = await Json(client, Url);
        Assert.Equal(before.GetProperty("totals").GetProperty("revenue").GetDecimal(), after.GetProperty("totals").GetProperty("revenue").GetDecimal());
        Assert.Equal(before.GetProperty("totals").GetProperty("failedCount").GetInt32() + 1, after.GetProperty("totals").GetProperty("failedCount").GetInt32());
        Assert.Equal(before.GetProperty("totals").GetProperty("pendingCount").GetInt32() - 1, after.GetProperty("totals").GetProperty("pendingCount").GetInt32());
    }

    [Fact]
    public async Task PendingExpiryIsReflectedEvenBeforeTheWorkerWritesOrInvalidates()
    {
        using var factory = WithCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        await RevenueTests.Seeded(factory);
        using var client = factory.Client("Admin");
        var before = await Json(client, Url);
        factory.Clock.Now = factory.Clock.Now.AddDays(2);
        var after = await Json(client, Url);
        Assert.Equal(0, after.GetProperty("totals").GetProperty("pendingCount").GetInt32());
        Assert.Equal(before.GetProperty("totals").GetProperty("expiredCount").GetInt32() + before.GetProperty("totals").GetProperty("pendingCount").GetInt32(),
            after.GetProperty("totals").GetProperty("expiredCount").GetInt32());
    }

    [Fact]
    public void RedisIsDisabledWhenNotConfigured()
    {
        using var factory = new BillingApiFactory();
        Assert.IsType<NoRevenueCache>(factory.Services.GetRequiredService<IRevenueCache>());
    }

    private static BillingApiFactory WithCache(IDistributedCache storage) => new()
    {
        ExtraServices = services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddSingleton(storage);
            services.RemoveAll<IRevenueCache>();
            services.AddSingleton<IRevenueCache, RevenueCache>();
        }
    };

    // Test-only cache that always fails, like an unreachable Redis.
    private sealed class BrokenCache : IDistributedCache
    {
        private static Exception Fail() => new InvalidOperationException("Simulated Redis outage.");
        public byte[]? Get(string key) => throw Fail();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Fail();
        public void Refresh(string key) => throw Fail();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Fail();
        public void Remove(string key) => throw Fail();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Fail();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Fail();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Fail();
    }
}
