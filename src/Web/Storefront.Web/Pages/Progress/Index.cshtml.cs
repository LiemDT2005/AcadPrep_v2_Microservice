using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Progress;

public class IndexModel(ToeicApiClient api) : PageModel
{
    public ProgressSummary? Summary { get; set; }
    public async Task OnGetAsync()
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("jwt")))
            Summary = await api.GetProgressAsync();
    }
}
