using System.Text.Json;
using Billing.API.DTOs;
using Billing.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace Billing.API.Services;

// Redis cache for revenue statistics (TTL 1 hour). Keys embed a version stamp: InvalidateAsync (called by a
// successful IPN in the VNPay task) replaces the stamp, so every older entry stops being read at once.
// Any cache failure is logged and treated as a miss: statistics then come straight from the database.
public class RevenueCache(IDistributedCache cache, ILogger<RevenueCache> logger) : IRevenueCache
{
    public const string Prefix = "billing:revenue:";
    public const string VersionKey = Prefix + "version";
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<RevenueDto?> GetAsync(string key, CancellationToken ct)
    {
        try
        {
            var bytes = await cache.GetAsync(await KeyAsync(key, ct), ct);
            return bytes is null ? null : JsonSerializer.Deserialize<RevenueDto>(bytes, JsonOpts);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Revenue cache read failed; using the database. Reason={Reason}", exception.GetType().Name);
            return null;
        }
    }

    public async Task SetAsync(string key, RevenueDto value, CancellationToken ct)
    {
        try
        {
            await cache.SetAsync(await KeyAsync(key, ct), JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Revenue cache write failed. Reason={Reason}", exception.GetType().Name);
        }
    }

    public async Task InvalidateAsync(CancellationToken ct)
    {
        try { await cache.SetStringAsync(VersionKey, Guid.NewGuid().ToString("N"), ct); }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Stale entries then expire by TTL; the caller's payment must never fail because of the cache.
            logger.LogWarning("Revenue cache invalidation failed. Reason={Reason}", exception.GetType().Name);
        }
    }

    private async Task<string> KeyAsync(string key, CancellationToken ct) =>
        $"{Prefix}v{await cache.GetStringAsync(VersionKey, ct) ?? "0"}:{key}";
}

// Used when Redis is not configured (local F5 without Redis): no caching, always the database.
public sealed class NoRevenueCache : IRevenueCache
{
    public Task<RevenueDto?> GetAsync(string key, CancellationToken ct) => Task.FromResult<RevenueDto?>(null);
    public Task SetAsync(string key, RevenueDto value, CancellationToken ct) => Task.CompletedTask;
    public Task InvalidateAsync(CancellationToken ct) => Task.CompletedTask;
}
