using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Notifications;

public abstract class NotificationPageModel(ToeicApiClient api) : PageModel
{
    protected ToeicApiClient Api { get; } = api;
    protected virtual bool AdminOnly => false;
    public string? Error { get; set; }
    public bool CanSendNotifications { get; private set; }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("jwt")))
        {
            context.Result = RedirectToPage("/Account/Login");
            return;
        }
        var identity = await Api.GetNotificationIdentityAsync(HttpContext.RequestAborted);
        if (identity.Status == 401)
        {
            HttpContext.Session.Remove("jwt");
            context.Result = RedirectToPage("/Account/Login");
            return;
        }
        if (identity.Status == 403 || identity.Success && AdminOnly && identity.Data?.Roles.Contains("Admin") != true)
        {
            context.Result = StatusCode(403);
            return;
        }
        if (!identity.Success || !Guid.TryParse(identity.Data?.Id, out var id) || id == Guid.Empty)
        {
            Error = "Không thể xác minh phiên đăng nhập. Vui lòng thử lại sau.";
            Response.StatusCode = 503;
            context.Result = Page();
            return;
        }
        CanSendNotifications = identity.Data?.Roles.Contains("Admin") == true;
        await next();
    }

    protected IActionResult? AuthenticationFailure(int status)
    {
        if (status == 401)
        {
            HttpContext.Session.Remove("jwt");
            return RedirectToPage("/Account/Login");
        }
        return status == 403 ? StatusCode(403) : null;
    }
}
