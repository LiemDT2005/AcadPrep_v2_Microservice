namespace Billing.API.Services;

public static class BillingFeatures
{
    public static readonly IReadOnlyList<string> All = ["FullTest", "Practice", "Vocab"];
}

// Timestamps are stored and returned in UTC; day/month boundaries follow Vietnam time (UTC+7, no DST).
public static class UsagePeriods
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateTime? ResetAt(string period, DateTime utcNow)
    {
        var local = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc) + VietnamOffset;
        var dayStart = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Utc);
        return period switch
        {
            "Daily" => dayStart.AddDays(1) - VietnamOffset,
            "Monthly" => new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1) - VietnamOffset,
            "Lifetime" => null,
            _ => throw new PricingConfigurationException()
        };
    }
}
