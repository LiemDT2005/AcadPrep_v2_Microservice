using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Billing.Orders;

// UC-21.3: the Learner's own orders. Orders exist only after checkout (VNPay task); no sample data.
public class IndexModel(ToeicApiClient identity, BillingApiClient billing) : BillingPageModel(identity, billing)
{
    private static readonly string[] Statuses = ["Pending", "Paid", "Failed", "Expired"];

    protected override string RequiredRole => "Learner";
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;
    public BillingPage<BillingOrderItem>? Orders { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Status is not null && !Statuses.Contains(Status)) Status = null;
        if (PageIndex < 1) PageIndex = 1;
        var result = await Billing.GetMyOrdersAsync(Status, PageIndex, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (!result.Success)
        {
            Response.StatusCode = result.Status == 503 ? 503 : 502;
            Error = "Chưa thể tải lịch sử đơn hàng. Vui lòng thử lại sau.";
            return Page();
        }
        Orders = result.Data;
        return Page();
    }
}
