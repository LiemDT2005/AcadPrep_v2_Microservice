using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class ProfileModel(ToeicApiClient api) : PageModel
{
    public UserProfileDto? UserProfile { get; set; }

    [BindProperty]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    public string? Avatar { get; set; }

    [BindProperty]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Account/Login");
        }

        UserProfile = await api.GetProfileAsync();
        if (UserProfile is not null)
        {
            FullName = UserProfile.FullName;
            Avatar = UserProfile.Avatar;
        }
        else
        {
            var userId = HttpContext.Session.GetString("userId") ?? "N/A";
            var name = HttpContext.Session.GetString("name") ?? "User";
            var avatar = HttpContext.Session.GetString("avatar");
            UserProfile = new UserProfileDto(
                Id: userId,
                Email: "Loading...",
                FullName: name,
                Avatar: avatar,
                Status: "Active",
                Roles: ["Learner"],
                CreatedAt: DateTime.UtcNow,
                LastLoginAt: DateTime.UtcNow
            );
            FullName = name;
            Avatar = avatar;
        }

        return Page();
    }

    public async Task<IActionResult> OnGetProfileJsonAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
            return new JsonResult(new { error = "Not authenticated." }) { StatusCode = StatusCodes.Status401Unauthorized };

        var profile = await api.GetProfileAsync();
        if (profile is null)
            return NotFound(new { error = "User profile not found." });

        return new JsonResult(profile);
    }

    public async Task<IActionResult> OnPostUpdateProfileAsync()
    {
        var isAjax = Request.Headers.XRequestedWith == "XMLHttpRequest" ||
                     Request.Headers.Accept.ToString().Contains("application/json");

        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
        {
            if (isAjax) return new JsonResult(new { error = "Session expired. Please sign in again." }) { StatusCode = StatusCodes.Status401Unauthorized };
            return RedirectToPage("/Account/Login");
        }

        if (string.IsNullOrWhiteSpace(FullName) || FullName.Trim().Length < 2)
        {
            var msg = "Full name must be at least 2 characters.";
            if (isAjax) return new JsonResult(new { error = msg }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = msg;
            return RedirectToPage();
        }

        var cleanAvatar = string.IsNullOrWhiteSpace(Avatar) ? null : Avatar.Trim();
        var (data, error) = await api.UpdateProfileAsync(FullName.Trim(), cleanAvatar);
        if (error is not null)
        {
            if (isAjax) return new JsonResult(new { error }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = error;
        }
        else
        {
            var msg = "Profile updated successfully!";
            if (isAjax) return new JsonResult(new { success = true, message = msg, data });
            SuccessMessage = msg;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        var isAjax = Request.Headers.XRequestedWith == "XMLHttpRequest" ||
                     Request.Headers.Accept.ToString().Contains("application/json");

        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
        {
            if (isAjax) return new JsonResult(new { error = "Session expired. Please sign in again." }) { StatusCode = StatusCodes.Status401Unauthorized };
            return RedirectToPage("/Account/Login");
        }

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            var msg = "Current password is required.";
            if (isAjax) return new JsonResult(new { error = msg }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = msg;
            return RedirectToPage();
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            var msg = "New password must be at least 6 characters.";
            if (isAjax) return new JsonResult(new { error = msg }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = msg;
            return RedirectToPage();
        }

        if (NewPassword != ConfirmPassword)
        {
            var msg = "Passwords do not match.";
            if (isAjax) return new JsonResult(new { error = msg }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = msg;
            return RedirectToPage();
        }

        var (success, error) = await api.ChangePasswordAsync(CurrentPassword, NewPassword, ConfirmPassword);
        if (!success)
        {
            var msg = error ?? "Failed to change password.";
            if (isAjax) return new JsonResult(new { error = msg }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = msg;
        }
        else
        {
            var msg = "Password changed successfully!";
            if (isAjax) return new JsonResult(new { success = true, message = msg });
            SuccessMessage = msg;
        }

        return RedirectToPage();
    }
}
