using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Billing.API.Formatters;

namespace Billing.API.DTOs;

public class RevenueQuery
{
    // Vietnam calendar dates, inclusive. Defaults: the last 30 days including today (VN).
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    [RegularExpression("^(day|month)$")]
    public string GroupBy { get; init; } = "day";
}

// paidCount/failedCount/pendingCount/expiredCount count orders CREATED in the range (spec);
// revenue sums Paid orders by PaidAt. Each point's paidCount counts orders PAID in that period.
public record RevenueTotalsDto(decimal Revenue, int PaidCount, int FailedCount, int PendingCount, int ExpiredCount);

public record RevenuePointDto(string Period, decimal Revenue, int PaidCount);

public record RevenueDto(DateOnly From, DateOnly To, string GroupBy, RevenueTotalsDto Totals,
    IReadOnlyList<RevenuePointDto> Points);

public sealed class RevenueCsvExport(RevenueDto revenue) : ICsvExportable
{
    public string FileName => $"revenue-{revenue.GroupBy}-{revenue.From:yyyy-MM-dd}-{revenue.To:yyyy-MM-dd}.csv";
    public IReadOnlyList<string> Header { get; } = ["period", "revenue", "paidCount"];

    public IEnumerable<IReadOnlyList<string?>> Rows => revenue.Points.Select(point => (IReadOnlyList<string?>)
    [
        point.Period, point.Revenue.ToString("0", CultureInfo.InvariantCulture),
        point.PaidCount.ToString(CultureInfo.InvariantCulture)
    ]);
}
