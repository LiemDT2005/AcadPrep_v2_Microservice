using Billing.API.Services;
using Xunit;

namespace Billing.API.Tests;

public class UsagePeriodTests
{
    private static DateTime Utc(int y, int m, int d, int h, int min = 0) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

    [Theory]
    // 17:30 UTC 18/10 is 00:30 19/10 in Vietnam: next daily reset is 00:00 20/10 VN = 17:00 UTC 19/10.
    [InlineData(2026, 10, 18, 17, 30, 2026, 10, 19, 17)]
    // 16:59 UTC 18/10 is still 23:59 18/10 VN: reset at 00:00 19/10 VN = 17:00 UTC 18/10.
    [InlineData(2026, 10, 18, 16, 59, 2026, 10, 18, 17)]
    // Exactly 17:00 UTC is VN midnight: the next reset is a full day later.
    [InlineData(2026, 10, 18, 17, 0, 2026, 10, 19, 17)]
    public void DailyResetFollowsVietnamMidnight(int y, int m, int d, int h, int min, int ry, int rm, int rd, int rh) =>
        Assert.Equal(Utc(ry, rm, rd, rh), UsagePeriods.ResetAt("Daily", Utc(y, m, d, h, min)));

    [Theory]
    [InlineData(2026, 10, 31, 16, 59, 2026, 10, 31, 17)] // 23:59 31/10 VN -> 00:00 01/11 VN
    [InlineData(2026, 10, 31, 17, 0, 2026, 11, 30, 17)]  // already 01/11 VN -> 00:00 01/12 VN
    [InlineData(2026, 12, 31, 18, 0, 2027, 1, 31, 17)]   // 01/01/2027 VN -> 00:00 01/02/2027 VN
    [InlineData(2026, 12, 15, 0, 0, 2026, 12, 31, 17)]   // year boundary
    public void MonthlyResetFollowsVietnamFirstOfMonth(int y, int m, int d, int h, int min, int ry, int rm, int rd, int rh) =>
        Assert.Equal(Utc(ry, rm, rd, rh), UsagePeriods.ResetAt("Monthly", Utc(y, m, d, h, min)));

    [Fact]
    public void LifetimeNeverResetsAndUnknownIsConfigurationError()
    {
        Assert.Null(UsagePeriods.ResetAt("Lifetime", DateTime.UtcNow));
        Assert.Throws<PricingConfigurationException>(() => UsagePeriods.ResetAt("Weekly", DateTime.UtcNow));
    }
}
