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
        var result = await api.GetUserStatsAsync();
        if (result != null)
        {
            Stats = result;
        }

        return Page();
    }
}
