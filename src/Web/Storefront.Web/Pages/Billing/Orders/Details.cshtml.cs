using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Billing.Orders;

// UC-21.3 detail. Also the future payment-result source: status always comes from the API, never the URL.
public class DetailsModel(ToeicApiClient identity, BillingApiClient billing) : BillingPageModel(identity, billing)
{
    protected override string RequiredRole => "Learner";
    [BindProperty(SupportsGet = true)] public string? OrderCode { get; set; }
    public BillingOrderDetail? Order { get; private set; }
    public bool NotFoundOrder { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(OrderCode)) return MissingOrder();
        var result = await Billing.GetMyOrderAsync(OrderCode.Trim(), ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Status == 404) return MissingOrder();
        if (!result.Success)
        {
            Response.StatusCode = result.Status == 503 ? 503 : 502;
            Error = "Chưa thể tải đơn hàng. Vui lòng thử lại sau.";
            return Page();
        }
        Order = result.Data;
        return Page();
    }

    private IActionResult MissingOrder()
    {
        NotFoundOrder = true;
        Response.StatusCode = 404;
        return Page();
    }
}
