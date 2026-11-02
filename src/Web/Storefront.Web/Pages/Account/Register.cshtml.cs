using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class RegisterModel(ToeicApiClient api) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Please confirm your password.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Password != ConfirmPassword)
        {
            Error = "Passwords do not match.";
            return Page();
        }

        var (data, error) = await api.RegisterAsync(Email, Password, FullName, ConfirmPassword);
        if (data is null)
        {
            Error = error ?? "Registration failed. Please try again.";
            return Page();
        }

        TempData["SuccessMessage"] = data.Message;
        return RedirectToPage("/Account/VerifyOtp", new { email = data.Email });
    }
}
