using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class ForgotPasswordModel(ToeicApiClient api) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var (success, msg) = await api.ForgotPasswordAsync(Email.Trim());
        if (!success)
        {
            ErrorMessage = msg;
            return Page();
        }

        TempData["SuccessMessage"] = "A password reset OTP code has been dispatched to your email. Please check your inbox!";
        return RedirectToPage("/Account/ResetPassword", new { email = Email.Trim() });
    }
}
