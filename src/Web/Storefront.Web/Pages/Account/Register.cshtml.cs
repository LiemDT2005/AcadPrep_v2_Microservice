using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class RegisterModel(ToeicApiClient api) : PageModel
{
    [BindProperty] public string FullName { get; set; } = string.Empty;
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var (data, error) = await api.RegisterAsync(Email, Password, FullName);
        if (data is null)
        {
            Error = error ?? "Đăng ký thất bại.";
            return Page();
        }
        HttpContext.Session.SetString("jwt", data.Token);
        HttpContext.Session.SetString("name", data.FullName);
        HttpContext.Session.SetString("userId", data.UserId);
        return RedirectToPage("/Courses/Index");
    }
}
