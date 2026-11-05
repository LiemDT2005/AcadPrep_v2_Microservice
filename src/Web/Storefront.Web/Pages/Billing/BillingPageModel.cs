using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Billing;

// Session guard for private Billing pages. Roles come from Identity, never from decoding the JWT here.
// The Billing API authorizes every call again; this guard only drives UX.
public abstract class BillingPageModel(ToeicApiClient identity, BillingApiClient billing) : PageModel
{
    protected BillingApiClient Billing { get; } = billing;
    // Each role has one job: learner pages require Learner, admin pages require Admin.
    protected abstract string RequiredRole { get; }
    public string? Error { get; set; }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Session.GetString("jwt")))
        {
            context.Result = RedirectToBillingLogin();
            return;
        }
        var me = await identity.GetNotificationIdentityAsync(HttpContext.RequestAborted);
        if (me.Status == 401)
        {
            HttpContext.Session.Remove("jwt");
            context.Result = RedirectToBillingLogin();
            return;
        }
        if (me.Status == 403 || me.Success && me.Data?.Roles.Contains(RequiredRole) != true)
        {
            context.Result = StatusCode(403);
            return;
        }
        if (!me.Success || !Guid.TryParse(me.Data?.Id, out var id) || id == Guid.Empty)
        {
            Error = "Không thể xác minh phiên đăng nhập. Vui lòng thử lại sau.";
            Response.StatusCode = 503;
            context.Result = Page();
            return;
        }
        await next();
    }

    protected IActionResult? AuthenticationFailure(int status)
    {
        if (status == 401)
        {
            HttpContext.Session.Remove("jwt");
            return RedirectToBillingLogin();
        }
        return status == 403 ? StatusCode(403) : null;
    }

    private RedirectToPageResult RedirectToBillingLogin() =>
        RedirectToPage("/Account/Login", new { returnUrl = Request.GetEncodedPathAndQuery() });
}
