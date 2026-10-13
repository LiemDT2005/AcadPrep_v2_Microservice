using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class LogoutModel(ToeicApiClient api) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await api.LogoutAsync();
        return RedirectToPage("/Account/Login");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await api.LogoutAsync();
        return RedirectToPage("/Account/Login");
    }
}
