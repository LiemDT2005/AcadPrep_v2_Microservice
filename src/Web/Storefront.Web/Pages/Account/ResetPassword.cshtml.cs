using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class ResetPasswordModel(ToeicApiClient api) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Please enter the 6-digit OTP code.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP code must be exactly 6 digits.")]
    public string Otp { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "New password is required.")]
    [MinLength(6, ErrorMessage = "New password must be at least 6 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [TempData]
    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToPage("/Account/ForgotPassword");
        }

        Email = email.Trim();
        return Page();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var (success, error) = await api.ResetPasswordAsync(Email.Trim(), Otp.Trim(), NewPassword, ConfirmPassword);
        if (!success)
        {
            ErrorMessage = error ?? "Password reset failed.";
            return Page();
        }

        TempData["SuccessMessage"] = "Password reset successful! Please sign in with your new password.";
        return RedirectToPage("/Account/Login");
    }

    public async Task<IActionResult> OnPostResendOtpAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Email address not found.";
            return Page();
        }

        var (success, message) = await api.ResendOtpAsync(Email.Trim());
        if (!success)
        {
            ErrorMessage = message;
        }
        else
        {
            SuccessMessage = "A new OTP code has been sent to your email.";
        }

        return Page();
    }
}
