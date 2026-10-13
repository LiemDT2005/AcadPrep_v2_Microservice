using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class RegisterModel(ToeicApiClient api) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [MinLength(2, ErrorMessage = "Họ tên phải có ít nhất 2 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên.")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Vui lòng xác nhận lại mật khẩu.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Password != ConfirmPassword)
        {
            Error = "Mật khẩu xác nhận không khớp.";
            return Page();
        }

        var (data, error) = await api.RegisterAsync(Email, Password, FullName, ConfirmPassword);
        if (data is null)
        {
            Error = error ?? "Đăng ký không thành công. Vui lòng thử lại.";
            return Page();
        }

        // Chuyển sang trang VerifyOtp và truyền email kèm thông báo
        TempData["SuccessMessage"] = data.Message;
        return RedirectToPage("/Account/VerifyOtp", new { email = data.Email });
    }
}
