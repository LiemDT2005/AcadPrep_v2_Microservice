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

    // Recipient suggestions (any role, any status, same rule as the backend recipient check).
    public Task<IActionResult> OnGetUsersAsync(string? q) => Storefront.Web.Pages.Admin.AdminUserLookup.SearchAsync(Api, q, null, null);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        var result = await Api.CreateNotificationAsync(Input, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (result.Success)
        {
            Notice = "Notification sent successfully.";
            return RedirectToPage();
        }
        // Format/audience rules are validated on this page first, so a 422 on targetUserId means the
        // backend could not find the recipient at Identity.
        if (result.Status == 422 && Input.Audience == "User" &&
            result.Errors?.Keys.Any(key => key.Equals("targetUserId", StringComparison.OrdinalIgnoreCase)) == true)
        {
            ModelState.AddModelError("Input.TargetUserId", "Người dùng không tồn tại.");
            Error = "Không tìm thấy người nhận. Vui lòng kiểm tra ID người dùng.";
        }
        else if (result.Status == 503)
            Error = "Chưa thể xác minh người nhận. Vui lòng thử lại sau.";
        else if (result.Status is 400 or 422)
        {
            foreach (var field in result.Errors ?? new Dictionary<string, string[]>())
            {
                var property = new[] { "Title", "Content", "Audience", "TargetUserId" }
                    .FirstOrDefault(name => name.Equals(field.Key, StringComparison.OrdinalIgnoreCase));
                if (property is not null) ModelState.AddModelError($"Input.{property}", string.Join(" ", field.Value));
            }
            Error = "Please check the form fields and try again.";
        }
        else Error = "Unable to send the notification. Please try again later.";
        return Page();
    }
}
