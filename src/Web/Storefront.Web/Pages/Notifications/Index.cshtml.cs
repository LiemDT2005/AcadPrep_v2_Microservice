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
        if (!ModelState.IsValid) { Error = "Invalid pagination or filter parameters."; Response.StatusCode = 400; return Page(); }
        var result = await Api.GetNotificationsAsync(PageIndex, PageSize, UnreadOnly, ct);
        var failure = AuthenticationFailure(result.Status);
        if (failure is not null) return failure;
        if (!result.Success) Error = "Unable to load notifications. Please try again.";
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
        Notice = result.Success ? (delete ? "Notification removed from your list." : "Marked as read.")
            : result.Status == 404 ? "This notification is no longer available." : "The action could not be completed. Please try again.";
        return RedirectToPage(new { PageIndex, PageSize, UnreadOnly });
    }
}
