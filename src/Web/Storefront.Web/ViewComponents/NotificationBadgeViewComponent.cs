using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Services;

namespace Storefront.Web.ViewComponents;

public class NotificationBadgeViewComponent(ToeicApiClient api) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("jwt"))) return Content(string.Empty);
        var result = await api.GetNotificationCountAsync(HttpContext.RequestAborted);
        int? count = result.Success && result.Data?.UnreadCount >= 0 ? result.Data.UnreadCount : null;
        return View(count);
    }
}
