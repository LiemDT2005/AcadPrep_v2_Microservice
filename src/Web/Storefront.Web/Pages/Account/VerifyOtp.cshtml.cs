using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class VerifyOtpModel(ToeicApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "OTP code is required.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP code must be exactly 6 digits.")]
    public string Otp { get; set; } = string.Empty;

    public string? Error { get; set; }
    public string? SuccessMessage { get; set; }

    public void OnGet()
    {
        if (TempData["SuccessMessage"] is string msg)
        {
            SuccessMessage = msg;
        }
    }

    public async Task<IActionResult> OnPostVerifyAsync()
    {
        if (!ModelState.IsValid)
        {
            Error = "Please enter all 6 digits of the OTP code.";
            return new JsonResult(new { success = false, error = Error });
        }

        var (data, error) = await api.VerifyOtpAsync(Email, Otp);
        if (data is null)
        {
            return new JsonResult(new { success = false, error = error ?? "Verification failed. Please check the OTP code." });
        }

        HttpContext.Session.SaveAuthentication(data);

        return new JsonResult(new { success = true, redirectTo = Url.Page("/Index") });
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || !new EmailAddressAttribute().IsValid(Email))
            return new JsonResult(new { success = false, error = "Invalid email address." });
        var (success, message) = await api.ResendOtpAsync(Email.Trim());
        return new JsonResult(new { success, error = success ? null : message });
    }
}
