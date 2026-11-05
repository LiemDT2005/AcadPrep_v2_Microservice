using System.Globalization;

namespace Storefront.Web.Pages.Billing.Orders;

// Display helpers shared by the Billing order pages. Times are shown in UTC+7 (Vietnam, no DST).
public static class OrderText
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Status(string status) => status switch
    {
        "Pending" => "Chờ thanh toán", "Paid" => "Đã thanh toán", "Failed" => "Thất bại", "Expired" => "Hết hạn",
        _ => status
    };

    public static string Kind(string kind) => kind switch { "Purchase" => "Mua mới", "Renewal" => "Gia hạn", _ => kind };

    public static string Money(decimal amount) => amount.ToString("N0", Vi) + " đ";

    public static string Date(DateTimeOffset? value) => value is null ? "—"
        : value.Value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm", Vi);
}
