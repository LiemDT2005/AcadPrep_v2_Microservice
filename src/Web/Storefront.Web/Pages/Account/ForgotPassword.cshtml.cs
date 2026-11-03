using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class ForgotPasswordModel(ToeicApiClient api) : PageModel
{
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string OtpCode { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;
    public int Step { get; private set; } = 1;
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet(int step = 1)
    {
        if (step == 2)
        {
            Email = HttpContext.Session.GetString("resetEmail") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Email)) return RedirectToPage();
            Step = 2;
            SuccessMessage = "Check your inbox for the password reset code.";
        }
        return Page();
    }

    public Task<IActionResult> OnPostAsync() => OnPostSendOtpAsync();

    public async Task<IActionResult> OnPostSendOtpAsync()
    {
        Email = Email?.Trim() ?? string.Empty;
        if (!new EmailAddressAttribute().IsValid(Email))
        {
            ErrorMessage = "Enter a valid email address.";
            return Page();
        }
        var (success, message) = await api.ForgotPasswordAsync(Email);
        if (!success) { ErrorMessage = message; return Page(); }
        HttpContext.Session.SetString("resetEmail", Email);
        return RedirectToPage(new { step = 2 });
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        Email = HttpContext.Session.GetString("resetEmail") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Email)) return RedirectToPage();
        Step = 2;
        OtpCode ??= string.Empty;
        if (!System.Text.RegularExpressions.Regex.IsMatch(OtpCode, @"^\d{6}$") || string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6 || NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Enter the six-digit code and matching new passwords.";
            return Page();
        }
        var (success, error) = await api.ResetPasswordAsync(Email, OtpCode, NewPassword, ConfirmPassword);
        if (!success) { ErrorMessage = error; return Page(); }
        HttpContext.Session.Remove("resetEmail");
        TempData["SuccessMessage"] = "Password reset successfully. Please sign in.";
        return RedirectToPage("/Account/Login");
    }
}
