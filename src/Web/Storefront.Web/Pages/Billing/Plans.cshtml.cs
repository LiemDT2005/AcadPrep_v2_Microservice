using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Billing;

public class PlansModel(BillingApiClient api) : PageModel
{
    public IReadOnlyList<BillingPlan> Plans { get; private set; } = [];
    public string? Error { get; private set; }
    // The signed-in Learner's effective plan (Active paid plan or FREE); null for guests, staff or when Billing
    // cannot answer, in which case the price list is shown without a "current plan" marker.
    public BillingSubscription? Current { get; private set; }
    public BillingUsage? Usage { get; private set; }
    public string? UsageError { get; private set; }
    public string? SubscriptionError { get; private set; }
    public string? PaymentError { get; private set; }
    public bool IsSignedIn => !string.IsNullOrWhiteSpace(HttpContext.Session.GetString("jwt"));
    public bool IsLearner => IsSignedIn &&
        (HttpContext.Session.GetString("roles") ?? "").Split(',', StringSplitOptions.TrimEntries).Contains("Learner");

    public async Task OnGetAsync(CancellationToken ct)
    {
        var result = await api.GetPlansAsync(ct);
        if (!result.Success)
        {
            Response.StatusCode = result.Status == 503 ? 503 : 502;
            Error = "Chưa thể tải bảng giá. Vui lòng thử lại sau.";
            return;
        }
        Plans = result.Data ?? [];
        if (IsLearner)
        {
            var subscription = await api.GetMySubscriptionAsync(ct);
            if (subscription.Success) Current = subscription.Data;
            else SubscriptionError = "Chưa thể tải thông tin gói. Vui lòng thử lại sau.";
            var usage = await api.GetMyUsageAsync(ct);
            if (usage.Success) Usage = usage.Data;
            else UsageError = "Chưa thể tải hạn mức sử dụng. Vui lòng thử lại sau.";
        }
    }

    public async Task<IActionResult> OnPostPayAsync(Guid planId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("jwt")))
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Billing/Plans") });
        if (!IsLearner) return StatusCode(403);

        await OnGetAsync(ct);
        if (!ModelState.IsValid || !Plans.Any(plan => plan.Id == planId && plan.Code != "FREE" && plan.Price > 0))
        {
            PaymentError = "Gói này hiện không thể thanh toán.";
            return Page();
        }
        if (Current is null)
        {
            PaymentError = "Chưa thể xác minh gói hiện tại. Vui lòng thử lại sau.";
            return Page();
        }

        var result = await api.CheckoutAsync(planId, Current.Status is "Active" or "Expired", ct);
        if (result.Status == 401)
        {
            HttpContext.Session.Remove("jwt");
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Billing/Plans") });
        }
        if (result.Status == 403) return StatusCode(403);
        if (result.Success && result.Data is { } checkout && CheckoutModel.IsVnpayUrl(checkout.PaymentUrl))
            return Redirect(checkout.PaymentUrl);

        PaymentError = result.Status == 503
            ? "Cổng thanh toán đang tạm thời không khả dụng. Vui lòng thử lại sau."
            : "Không thể mở thanh toán. Vui lòng thử lại sau.";
        return Page();
    }

    public bool IsCurrent(BillingPlan plan) => Current is not null &&
        string.Equals(Current.PlanCode, plan.Code, StringComparison.OrdinalIgnoreCase);
}
