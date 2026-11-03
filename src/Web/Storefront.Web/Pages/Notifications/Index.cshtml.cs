using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Notifications;

public class IndexModel(ToeicApiClient api) : NotificationPageModel(api)
{
    [BindProperty(SupportsGet = true), Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;
    [BindProperty(SupportsGet = true), Range(1, 100)]
    public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)]
    public bool UnreadOnly { get; set; }
    public NotificationPage? Notifications { get; private set; }
    [TempData] public string? Notice { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) { Error = "Tham số phân trang hoặc bộ lọc không hợp lệ."; Response.StatusCode = 400; return Page(); }
        var result = await Api.GetNotificationsAsync(PageIndex, PageSize, UnreadOnly, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (!result.Success) Error = "Không thể tải thông báo. Vui lòng thử lại.";
        else Notifications = result.Data;
        return Page();
    }

    public Task<IActionResult> OnPostReadAsync(Guid id, CancellationToken ct) => ChangeAsync(id, false, ct);
    public Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct) => ChangeAsync(id, true, ct);

    private async Task<IActionResult> ChangeAsync(Guid id, bool delete, CancellationToken ct)
    {
        if (!ModelState.IsValid || id == Guid.Empty) return BadRequest();
        var result = delete ? await Api.DeleteNotificationAsync(id, ct) : await Api.ReadNotificationAsync(id, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        Notice = result.Success ? (delete ? "Đã xóa thông báo khỏi danh sách cá nhân." : "Đã đánh dấu đã đọc.")
            : result.Status == 404 ? "Thông báo không còn khả dụng." : "Thao tác chưa thành công. Vui lòng thử lại.";
        return RedirectToPage(new { PageIndex, PageSize, UnreadOnly });
    }
}
