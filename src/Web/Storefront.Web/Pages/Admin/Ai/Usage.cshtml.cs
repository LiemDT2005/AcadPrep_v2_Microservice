using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Pages.Billing;
using Storefront.Web.Services;
using Storefront.Web.Validation;

namespace Storefront.Web.Pages.Admin.Ai;

// UC-22.2: current 24h usage windows, limits per plan (effective immediately) and reset with audit.
public class UsageModel(ToeicApiClient identity, BillingApiClient billing, AiApiClient ai) : BillingPageModel(identity, billing)
{
    public static readonly System.Globalization.CultureInfo Vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
    protected override string RequiredRole => "Admin";
    [BindProperty(SupportsGet = true), Display(Name = "ID người dùng"), BillingGuid] public string? UserId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageIndex { get; set; } = 1;
    [BindProperty] public LimitInput Limit { get; set; } = new();
    [TempData] public string? Notice { get; set; }
    public JsonElement? Rows { get; private set; }
    public List<(string PlanCode, long TokenLimit)> Limits { get; } = [];
    public string? LimitsError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await LoadAsync(ct) ?? Page();

    public async Task<IActionResult> OnPostLimitAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid || Limit.TokenLimit is null)
        {
            Error = "Hạn mức chưa hợp lệ.";
            return await LoadAsync(ct, skipUsage: false) ?? Page();
        }
        var result = await ai.UpdateLimitAsync(Limit.PlanCode!, Limit.TokenLimit.Value, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Success)
        {
            Notice = $"Đã cập nhật hạn mức {Limit.PlanCode}: {Limit.TokenLimit?.ToString("N0", Vi)} token / 24 giờ.";
            return RedirectToPage(new { UserId, PageIndex });
        }
        Error = result.Status is 400 or 404 ? "Hạn mức hoặc gói không hợp lệ." : "Không thể cập nhật hạn mức lúc này.";
        return await LoadAsync(ct) ?? Page();
    }

    public async Task<IActionResult> OnPostResetAsync(Guid resetUserId, CancellationToken ct)
    {
        var result = await ai.ResetUsageAsync(resetUserId, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        Notice = result.Success ? "Đã đặt lại hạn mức của người dùng." : "Không thể đặt lại hạn mức lúc này.";
        return RedirectToPage(new { UserId, PageIndex });
    }

    private async Task<IActionResult?> LoadAsync(CancellationToken ct, bool skipUsage = false)
    {
        if (PageIndex < 1) PageIndex = 1;
        var limits = await ai.AdminLimitsAsync(ct);
        var failure = AuthenticationFailure(limits.Status);
        if (failure is not null) return failure;
        if (limits.Success && limits.Body is { ValueKind: JsonValueKind.Array } array)
            foreach (var item in array.EnumerateArray())
                Limits.Add((item.GetProperty("planCode").GetString()!, item.GetProperty("tokenLimit").GetInt64()));
        else LimitsError = "Chưa tải được hạn mức.";

        if (skipUsage || ModelState[nameof(UserId)]?.ValidationState == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
            return null;
        Guid? filter = Guid.TryParse(UserId, out var id) ? id : null;
        var usage = await ai.AdminUsageAsync(filter, PageIndex, ct);
        failure = AuthenticationFailure(usage.Status);
        if (failure is not null) return failure;
        if (usage.Success) Rows = usage.Body;
        else Error ??= "Chưa tải được danh sách sử dụng AI.";
        return null;
    }

    public class LimitInput
    {
        [Required, RegularExpression("^(FREE|PRO_MONTH|PRO_QUARTER)$")] public string? PlanCode { get; set; }
        [Display(Name = "Hạn mức token / 24 giờ"), Required(ErrorMessage = AiTokenLimitAttribute.Message), AiTokenLimit]
        public long? TokenLimit { get; set; }
    }
}
