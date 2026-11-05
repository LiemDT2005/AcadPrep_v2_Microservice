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
    public bool IsModerator { get; private set; }
    protected bool IsAjax => Request.Headers.XRequestedWith == "XMLHttpRequest";

    private IActionResult AccessFailure(int status) => IsAjax
        ? new JsonResult(new { message = status == 401 ? "Phiên đăng nhập đã hết hạn." : "Bạn không có quyền thực hiện thao tác." }) { StatusCode = status }
        : status == 401 ? RedirectToPage("/Account/Login") : StatusCode(status);

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("jwt")))
        {
            context.Result = AccessFailure(401);
            return;
        }
        var identity = await Api.GetNotificationIdentityAsync(HttpContext.RequestAborted);
        if (identity.Status == 401)
        {
            HttpContext.Session.Remove("jwt");
            context.Result = AccessFailure(401);
            return;
        }
        if (identity.Status == 403 || identity.Success && AdminOnly && identity.Data?.Roles.Contains("Admin") != true)
        {
            context.Result = AccessFailure(403);
            return;
        }
        if (!identity.Success || !Guid.TryParse(identity.Data?.Id, out var id) || id == Guid.Empty)
        {
            Error = "Không thể xác minh phiên đăng nhập. Vui lòng thử lại sau.";
            Response.StatusCode = 503;
            context.Result = IsAjax ? new JsonResult(new { message = Error }) { StatusCode = 503 } : Page();
            return;
        }
        CanSendNotifications = identity.Data?.Roles.Contains("Admin") == true;
        IsModerator = identity.Data?.Roles.Contains("Moderator") == true;
        await next();
    }

    protected IActionResult? AuthenticationFailure(int status)
    {
        if (status == 401)
        {
            HttpContext.Session.Remove("jwt");
            return AccessFailure(401);
        }
        return status == 403 ? AccessFailure(403) : null;
    }
}
