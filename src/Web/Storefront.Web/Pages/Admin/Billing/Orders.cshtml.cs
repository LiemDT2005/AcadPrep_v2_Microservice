using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Storefront.Web.Models;
using Storefront.Web.Pages.Billing;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Billing;

// UC-21.4: Admin order list with filters, monthly CSV export, and the manual grant form.
public class OrdersModel(ToeicApiClient identity, BillingApiClient billing) : BillingPageModel(identity, billing)
{
    private static readonly string[] Fields = ["UserId", "PlanId", "DurationDays", "Note"];

    protected override string RequiredRole => "Admin";
    [BindProperty] public BillingGrantInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public BillingOrderFilter Filter { get; set; } = new();
    [TempData] public string? Notice { get; set; }
    public IReadOnlyList<BillingPlan> PaidPlans { get; private set; } = [];
    public bool PlansUnavailable { get; private set; }
    public BillingPage<BillingAdminOrder>? Orders { get; private set; }
    public string? OrdersError { get; private set; }

    // Grant recipients: only active Learners can receive a paid plan (Billing re-checks through Identity).
    public Task<IActionResult> OnGetUsersAsync(string? q) =>
        Storefront.Web.Pages.Admin.AdminUserLookup.SearchAsync(identity, q, "Learner", "Active");

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadPlansAsync(ct);
        return await LoadOrdersAsync(ct) ?? Page();
    }

    // Monthly CSV through the Billing API's custom output formatter.
    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(Filter.Month))
            ModelState.AddModelError("Filter.Month", "Chọn tháng cần xuất CSV.");
        if (!Valid("Filter"))
        {
            await LoadPlansAsync(ct);
            return await LoadOrdersAsync(ct, skipValidation: true) ?? Page();
        }
        var file = await Billing.DownloadAdminOrdersCsvAsync(Filter, ct);
        var failure = AuthenticationFailure(file.Status);
        if (failure is not null) return failure;
        if (file.Success && file.Data is { } csv) return File(csv.Content, "text/csv; charset=utf-8", csv.FileName);
        OrdersError = file.Status == 422
            ? "Tháng này có quá nhiều đơn để xuất. Hãy thu hẹp bộ lọc."
            : "Không thể xuất CSV lúc này. Vui lòng thử lại sau.";
        await LoadPlansAsync(ct);
        return await LoadOrdersAsync(ct, skipValidation: true) ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadPlansAsync(ct);
        if (!Valid("Input")) return await LoadOrdersAsync(ct) ?? Page();
        var result = await Billing.GrantSubscriptionAsync(Input, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Success && result.Data is { } granted)
        {
            Notice = $"Đã cấp gói. Hết hạn: {granted.EndDate.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm}.";
            return RedirectToPage();
        }
        foreach (var field in result.Errors ?? new Dictionary<string, string[]>())
        {
            var property = Fields.FirstOrDefault(name => name.Equals(field.Key, StringComparison.OrdinalIgnoreCase));
            if (property is not null) ModelState.AddModelError($"Input.{property}", string.Join(" ", field.Value));
        }
        // Keep the same RequestId on every failure so a retry of an ambiguous request stays idempotent.
        Error = result.Status switch
        {
            400 => "Dữ liệu chưa được chấp nhận. Vui lòng kiểm tra các trường.",
            404 => "Gói hoặc người dùng không tồn tại.",
            422 => "Chỉ có thể cấp gói trả phí cho tài khoản Learner đang hoạt động.",
            409 => "Gói của người dùng vừa thay đổi hoặc yêu cầu đã được dùng. Vui lòng tải lại trang và thử lại.",
            503 => "Chưa thể xác minh người dùng. Vui lòng thử lại sau.",
            _ => "Không thể cấp gói lúc này. Vui lòng thử lại sau."
        };
        return await LoadOrdersAsync(ct) ?? Page();
    }

    // Only the given form's fields decide validity: the list filter and the grant form share one page.
    private bool Valid(string prefix) => ModelState
        .Where(entry => entry.Key.StartsWith(prefix + ".", StringComparison.Ordinal))
        .All(entry => entry.Value.ValidationState != ModelValidationState.Invalid);

    private async Task<IActionResult?> LoadOrdersAsync(CancellationToken ct, bool skipValidation = false)
    {
        if (!skipValidation && !Valid("Filter")) return null;
        if (Filter.PageIndex < 1) Filter.PageIndex = 1;
        var result = await Billing.GetAdminOrdersAsync(Filter, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Success) Orders = result.Data;
        else OrdersError ??= result.Status == 400
            ? "Bộ lọc không hợp lệ."
            : "Chưa thể tải danh sách đơn hàng. Vui lòng thử lại sau.";
        return null;
    }

    private async Task LoadPlansAsync(CancellationToken ct)
    {
        var plans = await Billing.GetPlansAsync(ct);
        PlansUnavailable = !plans.Success;
        PaidPlans = (plans.Data ?? []).Where(plan => plan.Code != "FREE" && plan.Price > 0).ToArray();
    }

    public Dictionary<string, string> RouteFor(int pageIndex) => new()
    {
        ["Filter.Status"] = Filter.Status ?? "", ["Filter.UserId"] = Filter.UserId ?? "",
        ["Filter.PlanCode"] = Filter.PlanCode ?? "", ["Filter.From"] = Filter.From?.ToString("yyyy-MM-dd") ?? "",
        ["Filter.To"] = Filter.To?.ToString("yyyy-MM-dd") ?? "", ["Filter.SortBy"] = Filter.SortBy,
        ["Filter.SortDir"] = Filter.SortDir, ["Filter.PageIndex"] = pageIndex.ToString()
    };
}
