using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class LoginModel(ToeicApiClient api, IConfiguration configuration) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string? GoogleIdToken { get; set; }

    public string? Error { get; set; }
    public bool RequiresOtp { get; set; }
    public string? UnverifiedEmail { get; set; }
    public string GoogleClientId => configuration["Authentication:Google:ClientId"] ?? string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var (data, error, requiresOtp, unverifiedEmail) = await api.LoginAsync(Email, Password);
        if (data is null)
        {
            Error = error ?? "Invalid email or password.";
            RequiresOtp = requiresOtp;
            UnverifiedEmail = unverifiedEmail;
            return Page();
        }

        HttpContext.Session.SaveAuthentication(data);
        return RedirectAfterSignIn();
    }

    public async Task<IActionResult> OnPostGoogleAsync()
    {
        if (string.IsNullOrWhiteSpace(GoogleIdToken))
        {
            Error = "Google authentication credentials not found.";
            return Page();
        }

        var (data, error) = await api.GoogleLoginAsync(GoogleIdToken);
        if (data is null)
        {
            Error = error ?? "Google sign-in was unsuccessful.";
            return Page();
        }

        HttpContext.Session.SaveAuthentication(data);
        return RedirectAfterSignIn();
    }

    private IActionResult RedirectAfterSignIn()
    {
        if (Url.IsLocalUrl(ReturnUrl) && ReturnUrl is not ("/" or "/Index"))
            return LocalRedirect(ReturnUrl!);

        var roles = (HttpContext.Session.GetString("roles") ?? "").Split(',');
        var destination = roles.Contains("Admin") ? "/Admin/Dashboard"
            : roles.Contains("Moderator") ? "/Admin/Exams" : "/";
        return LocalRedirect(destination);
    }
}
