using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Notifications;

public class IndexModel(ToeicApiClient api) : NotificationPageModel(api)
{
    [BindProperty(SupportsGet = true), Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;
    [BindProperty(SupportsGet = true), Range(1, 100, ErrorMessage = "Số thông báo mỗi trang phải từ 1 đến 100.")]
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
        else
        {
            Notifications = result.Data;
            // A read under the unread filter, or a delete, may remove the last item of the last page.
            if (Notifications is { Items.Count: 0 } page && PageIndex > 1 && PageIndex > page.TotalPages)
            {
                PageIndex = Math.Max(1, page.TotalPages);
                result = await Api.GetNotificationsAsync(PageIndex, PageSize, UnreadOnly, ct);
                failure = AuthenticationFailure(result.Status);
                if (failure is not null) return failure;
                if (!result.Success) { Error = "Không thể tải thông báo. Vui lòng thử lại."; Notifications = null; }
                else Notifications = result.Data;
            }
        }
        return Page();
    }

    // Bell dropdown (Facebook-style): the newest items and the unread count, as JSON for notification-menu.js.
    public const int DropdownSize = 6;

    public async Task<IActionResult> OnGetLatestAsync(CancellationToken ct)
    {
        var page = await Api.GetNotificationsAsync(1, DropdownSize, false, ct);
        var failure = AuthenticationFailure(page.Status);
        if (failure is not null) return failure;
        var count = await Api.GetNotificationCountAsync(ct);
        if (!page.Success || page.Data is null || !count.Success || count.Data is null)
            return new JsonResult(new { message = "Không thể tải thông báo. Vui lòng thử lại." }) { StatusCode = 502 };
        return new JsonResult(new
        {
            unreadCount = count.Data.UnreadCount,
            totalCount = page.Data.TotalCount,
            items = page.Data.Items.Select(item => new { item.Id, item.Title, item.Content, item.Type, item.CreatedAt, item.IsRead })
        });
    }

    public Task<IActionResult> OnPostReadAsync(Guid id, CancellationToken ct) => ChangeAsync(id, false, ct);

    public async Task<IActionResult> OnPostReadAllAsync(CancellationToken ct)
    {
        var result = await Api.ReadAllNotificationsAsync(ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        var message = result.Success ? "Đã đánh dấu tất cả là đã đọc." : "Chưa đánh dấu được tất cả. Vui lòng thử lại.";
        if (IsAjax)
            return new JsonResult(new { message, refresh = result.Success }) { StatusCode = result.Success ? 200 : 502 };
        Notice = message;
        return RedirectToPage(new { PageIndex, PageSize, UnreadOnly });
    }
    public Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct) => ChangeAsync(id, true, ct);

    private async Task<IActionResult> ChangeAsync(Guid id, bool delete, CancellationToken ct)
    {
        if (!ModelState.IsValid || id == Guid.Empty)
            return IsAjax ? new JsonResult(new { message = "Dữ liệu thao tác không hợp lệ." }) { StatusCode = 400 } : BadRequest();
        var result = delete ? await Api.DeleteNotificationAsync(id, ct) : await Api.ReadNotificationAsync(id, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        var message = result.Success ? (delete ? "Đã xóa thông báo khỏi danh sách cá nhân." : "Đã đánh dấu đã đọc.")
            : result.Status == 404 ? "Thông báo không còn khả dụng." : "Thao tác chưa thành công. Vui lòng thử lại.";
        if (IsAjax)
            return new JsonResult(new { message, refresh = result.Success || result.Status == 404 })
            { StatusCode = result.Success ? 200 : result.Status == 404 ? 404 : 502 };
        Notice = message;
        return RedirectToPage(new { PageIndex, PageSize, UnreadOnly });
    }
}
