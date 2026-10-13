using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class VerifyOtpModel(ToeicApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP phải đúng 6 chữ số.")]
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
            Error = "Vui lòng nhập đúng 6 chữ số mã OTP.";
            return Page();
        }

        var (data, error) = await api.VerifyOtpAsync(Email, Otp);
        if (data is null)
        {
            Error = error ?? "Xác thực không thành công. Vui lòng kiểm tra lại mã OTP.";
            return Page();
        }

        // Lưu thông tin phiên đăng nhập
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

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            Error = "Địa chỉ email không hợp lệ.";
            return Page();
        }

        var (success, msg) = await api.ResendOtpAsync(Email);
        if (success)
        {
            SuccessMessage = "Mã OTP mới đã được gửi! Vui lòng kiểm tra hộp thư email.";
            Error = null;
        }
        else
        {
            Error = msg;
        }

        return Page();
    }
}
