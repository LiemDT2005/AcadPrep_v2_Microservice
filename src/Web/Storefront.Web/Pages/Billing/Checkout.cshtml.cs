using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Services;
using Storefront.Web.Validation;

namespace Storefront.Web.Pages.Billing;

// UC-21.2 checkout / UC-23.3 renewal. Price and duration are always shown and charged from the Billing API.
public class CheckoutModel(ToeicApiClient identity, BillingApiClient billing) : BillingPageModel(identity, billing)
{
    private static readonly string[] VnpayHosts = ["sandbox.vnpayment.vn", "vnpayment.vn", "pay.vnpay.vn"];

    protected override string RequiredRole => "Learner";
    [BindProperty] public BillingCheckoutInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public Guid? PlanId { get; set; }
    [BindProperty(SupportsGet = true)] public bool Renew { get; set; }
    public IReadOnlyList<BillingPlan> PaidPlans { get; private set; } = [];
    public bool PlansUnavailable { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        await LoadPlansAsync(ct);
        Input.PlanId = PlanId;
        Input.Renew = Renew;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadPlansAsync(ct);
        // Validate against the plans actually on sale for this request (custom ValidPlanForCheckout).
        Input.AllowedPlanIds = PaidPlans.Select(plan => plan.Id).ToArray();
        ModelState.Clear();
        if (PlansUnavailable || !TryValidateModel(Input, nameof(Input))) return Page();

        var result = await Billing.CheckoutAsync(Input.PlanId!.Value, Input.Renew, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Success && result.Data is { } checkout && IsVnpayUrl(checkout.PaymentUrl))
            return Redirect(checkout.PaymentUrl); // the same link again when a pending order is reused

        Error = (result.Status, result.Code) switch
        {
            (422, "NoSubscriptionToRenew") => "Bạn chưa có gói nào để gia hạn. Vui lòng mua gói mới.",
            (422, _) or (404, _) => "Gói này hiện không thể thanh toán.",
            (503, _) => "Cổng thanh toán đang tạm thời không khả dụng. Vui lòng thử lại sau.",
            (409, _) => "Đơn hàng vừa thay đổi. Vui lòng thử lại.",
            _ when result.Success => "Không thể mở cổng thanh toán. Vui lòng thử lại sau.",
            _ => "Không thể tạo đơn thanh toán. Vui lòng thử lại sau."
        };
        return Page();
    }

    // Never redirect anywhere but the VNPay gateway over HTTPS.
    internal static bool IsVnpayUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && VnpayHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    private async Task LoadPlansAsync(CancellationToken ct)
    {
        var plans = await Billing.GetPlansAsync(ct);
        PlansUnavailable = !plans.Success;
        PaidPlans = (plans.Data ?? []).Where(plan => plan.Code != "FREE" && plan.Price > 0).ToArray();
        HttpContext.Items[ValidPlanForCheckoutAttribute.AllowedItemKey] = PaidPlans.Select(plan => plan.Id).ToArray();
    }
}
