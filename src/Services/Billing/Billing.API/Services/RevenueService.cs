using System.Globalization;
using Billing.API.DTOs;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

public class RevenueService(IRevenueRepository revenue, IRevenueCache cache, TimeProvider clock) : IRevenueService
{
    public const int MaxDays = 366;
    public const int MaxMonths = 60;
    public const int DefaultDays = 30;

    public async Task<(RevenueDto? Value, string? Field, string? Error)> GetAsync(RevenueQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now + UsagePeriods.VietnamOffset);
        var to = query.To ?? (query.From is { } f ? Max(f, today) : today);
        var from = query.From ?? to.AddDays(-(DefaultDays - 1));
        if (from > to) return (null, nameof(query.From), "From must not be after To.");
        if (query.GroupBy == "day" && to.DayNumber - from.DayNumber + 1 > MaxDays)
            return (null, nameof(query.To), $"A daily range may span at most {MaxDays} days.");
        if (query.GroupBy == "month" && MonthIndex(to) - MonthIndex(from) + 1 > MaxMonths)
            return (null, nameof(query.To), $"A monthly range may span at most {MaxMonths} months.");

        var key = $"{query.GroupBy}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}";
        var fromUtc = StartUtc(from);
        var toExclusiveUtc = StartUtc(to.AddDays(1));
        // Status counts are live: even without a write, Pending becomes Expired as time passes.
        // Keep caching the paid revenue/series, but never return old counts from Redis.
        if (await cache.GetAsync(key, ct) is { } cached)
        {
            var currentCounts = await revenue.CountCreatedAsync(fromUtc, toExclusiveUtc, now, ct);
            return (cached with
            {
                Totals = cached.Totals with
                {
                    PaidCount = currentCounts.Paid, FailedCount = currentCounts.Failed,
                    PendingCount = currentCounts.Pending, ExpiredCount = currentCounts.Expired
                }
            }, null, null);
        }

        var paid = await revenue.GetPaidAsync(fromUtc, toExclusiveUtc, ct);
        var counts = await revenue.CountCreatedAsync(fromUtc, toExclusiveUtc, now, ct);

        var buckets = paid.GroupBy(item => Period(DateOnly.FromDateTime(item.PaidAt + UsagePeriods.VietnamOffset), query.GroupBy))
            .ToDictionary(group => group.Key, group => (Revenue: group.Sum(item => item.Amount), Count: group.Count()));
        // Every period in the range appears, empty ones with zero, so charts stay continuous.
        var points = Periods(from, to, query.GroupBy).Select(period => buckets.TryGetValue(period, out var bucket)
            ? new RevenuePointDto(period, bucket.Revenue, bucket.Count)
            : new RevenuePointDto(period, 0, 0)).ToArray();
        var result = new RevenueDto(from, to, query.GroupBy,
            new RevenueTotalsDto(paid.Sum(item => item.Amount), counts.Paid, counts.Failed, counts.Pending, counts.Expired),
            points);
        await cache.SetAsync(key, result, ct);
        return (result, null, null);
    }

    private static IEnumerable<string> Periods(DateOnly from, DateOnly to, string groupBy)
    {
        if (groupBy == "month")
            for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
                yield return Period(month, groupBy);
        else
            for (var day = from; day <= to; day = day.AddDays(1))
                yield return Period(day, groupBy);
    }

    private static string Period(DateOnly date, string groupBy) =>
        date.ToString(groupBy == "month" ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime StartUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) - UsagePeriods.VietnamOffset;

    private static int MonthIndex(DateOnly date) => date.Year * 12 + date.Month;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
