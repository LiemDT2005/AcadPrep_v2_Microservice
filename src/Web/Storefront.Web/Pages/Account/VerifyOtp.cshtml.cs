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
        if (string.IsNullOrWhiteSpace(Otp) || Otp.Length != 6)
        {
            Error = "Please enter all 6 digits of the OTP code.";
            return Page();
        }

        var (data, error) = await api.VerifyOtpAsync(Email, Otp);
        if (data is null)
        {
            Error = error ?? "Verification failed. Please check the OTP code.";
            return Page();
        }

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

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            Error = "Invalid email address.";
            return Page();
        }

        var (success, msg) = await api.ResendOtpAsync(Email);
        if (success)
        {
            SuccessMessage = "A new OTP code has been dispatched! Please check your email inbox.";
            Error = null;
        }
        else
        {
            Error = msg;
        }

        return Page();
    }
}
