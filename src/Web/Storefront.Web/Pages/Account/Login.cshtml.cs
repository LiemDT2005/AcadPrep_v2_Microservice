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
    public string GoogleClientId { get; set; } = string.Empty;

    public void OnGet()
    {
        GoogleClientId = configuration["Authentication:Google:ClientId"]
            ?? "401550501610-22kns7qtv5i7gda0bpqnn241a064u65m.apps.googleusercontent.com";
    }

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

        SaveSession(data);
        return RedirectToPage("/Index");
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

        SaveSession(data);
        return RedirectToPage("/Index");
    }

    private void SaveSession(Models.AuthResponse data)
    {
        HttpContext.Session.SetString("jwt", data.Token);
        HttpContext.Session.SetString("name", data.FullName);
        HttpContext.Session.SetString("userId", data.UserId);
        if (!string.IsNullOrEmpty(data.RefreshToken))
        {
            HttpContext.Session.SetString("refreshToken", data.RefreshToken);
        }
        if (!string.IsNullOrEmpty(data.Avatar))
        {
            HttpContext.Session.SetString("avatar", data.Avatar);
        }
        if (data.Roles is not null && data.Roles.Count > 0)
        {
            HttpContext.Session.SetString("roles", string.Join(",", data.Roles));
        }
    }
}
