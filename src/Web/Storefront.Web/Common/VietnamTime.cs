namespace Storefront.Web.Common;

public static class VietnamTime
{
    public static string Format(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7).ToString("dd/MM/yyyy HH:mm");
}
