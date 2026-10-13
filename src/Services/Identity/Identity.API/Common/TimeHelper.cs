namespace Identity.API.Common;

public static class TimeHelper
{
    /// <summary>
    /// Lấy thời gian hiện tại chuẩn múi giờ Việt Nam (UTC+7).
    /// Tương thích đa nền tảng cả Windows và Linux container.
    /// </summary>
    public static DateTime Now
    {
        get
        {
            try
            {
                var timeZoneId = OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh";
                var tzi = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzi);
            }
            catch
            {
                return DateTime.UtcNow.AddHours(7);
            }
        }
    }
}
