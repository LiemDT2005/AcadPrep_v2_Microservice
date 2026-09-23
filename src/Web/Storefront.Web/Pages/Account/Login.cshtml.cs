using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class LoginModel(ToeicApiClient api) : PageModel
{
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var (data, error) = await api.LoginAsync(Email, Password);
        if (data is null)
        {
            Error = error ?? "Sai email hoặc mật khẩu.";
            return Page();
        }
        HttpContext.Session.SetString("jwt", data.Token);
        HttpContext.Session.SetString("name", data.FullName);
        HttpContext.Session.SetString("userId", data.UserId);
        return RedirectToPage("/Index");
    }
}
