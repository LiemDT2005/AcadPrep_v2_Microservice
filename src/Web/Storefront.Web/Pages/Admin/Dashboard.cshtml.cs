using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin;

public class DashboardModel(ToeicApiClient api) : PageModel
{
    public UserStatsDto Stats { get; set; } = new(0, 0, 0, 0, 0, 0);

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("jwt")))
            return RedirectToPage("/Account/Login", new { returnUrl = "/Admin/Dashboard" });

        var roles = (HttpContext.Session.GetString("roles") ?? "").Split(',');
        if (!roles.Contains("Admin"))
            return roles.Contains("Moderator") ? RedirectToPage("/Admin/Exams/Index") : StatusCode(StatusCodes.Status403Forbidden);

        var result = await api.GetUserStatsAsync();
        if (result != null)
        {
            Stats = result;
        }

        return Page();
    }
}
