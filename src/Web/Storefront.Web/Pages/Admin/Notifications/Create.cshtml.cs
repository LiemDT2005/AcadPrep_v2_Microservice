using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Pages.Notifications;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Notifications;

public class CreateModel(ToeicApiClient api) : NotificationPageModel(api)
{
    protected override bool AdminOnly => true;
    [BindProperty] public NotificationInput Input { get; set; } = new();
    [TempData] public string? Notice { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        var result = await Api.CreateNotificationAsync(Input, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Success)
        {
            Notice = "Đã gửi thông báo thành công.";
            return RedirectToPage();
        }
        if (result.Status is 400 or 422)
        {
            foreach (var field in result.Errors ?? new Dictionary<string, string[]>())
            {
                var property = new[] { "Title", "Content", "Audience", "TargetUserId" }
                    .FirstOrDefault(name => name.Equals(field.Key, StringComparison.OrdinalIgnoreCase));
                if (property is not null) ModelState.AddModelError($"Input.{property}", string.Join(" ", field.Value));
            }
            Error = "Dữ liệu chưa được chấp nhận. Vui lòng kiểm tra các trường.";
        }
        else Error = "Không thể gửi thông báo. Vui lòng thử lại sau.";
        return Page();
    }
}
