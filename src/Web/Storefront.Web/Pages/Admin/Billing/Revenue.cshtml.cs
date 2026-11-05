using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Pages.Billing;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Billing;

// UC-21.5: revenue from Paid orders (by PaidAt) and order counts (by CreatedAt), from the Billing API.
public class RevenueModel(ToeicApiClient identity, BillingApiClient billing) : BillingPageModel(identity, billing)
{
    protected override string RequiredRole => "Admin";
    [BindProperty(SupportsGet = true)] public BillingRevenueFilter Filter { get; set; } = new();
    public BillingRevenue? Revenue { get; private set; }

    // Chart data serialized for billing-revenue.js; rendered inside <script type="application/json">.
    public string ChartJson => JsonSerializer.Serialize(new
    {
        labels = Revenue?.Points.Select(point => point.Period) ?? [],
        revenue = Revenue?.Points.Select(point => point.Revenue) ?? [],
        paid = Revenue?.Points.Select(point => point.PaidCount) ?? []
    });

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        var result = await Billing.GetRevenueAsync(Filter, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Success)
        {
            Revenue = result.Data;
            Filter.From ??= Revenue?.From;
            Filter.To ??= Revenue?.To;
        }
        else Error = result.Status == 400
            ? "Khoảng thời gian không hợp lệ (tối đa 366 ngày khi gom theo ngày, 60 tháng khi gom theo tháng)."
            : "Chưa thể tải thống kê doanh thu. Vui lòng thử lại sau.";
        return Page();
    }

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        var file = await Billing.DownloadRevenueCsvAsync(Filter, ct);
        var failure = AuthenticationFailure(file.Status);
        if (failure is not null) return failure;
        if (file.Success && file.Data is { } csv) return File(csv.Content, "text/csv; charset=utf-8", csv.FileName);
        Error = file.Status == 400
            ? "Khoảng thời gian không hợp lệ (tối đa 366 ngày khi gom theo ngày, 60 tháng khi gom theo tháng)."
            : "Không thể xuất CSV lúc này. Vui lòng thử lại sau.";
        return Page();
    }
}
