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

    public bool IsModerator => UserProfile?.Roles.Contains("Moderator") == true;
    public bool IsAdmin => UserProfile?.Roles.Contains("Admin") == true;
    public bool CanChangePassword => UserProfile?.CanChangePassword == true;
    public string ActiveTab { get; private set; } = "overview";
    public List<LearningRecordDto> RecentAttempts { get; private set; } = [];
    public List<ProfileMetric> Metrics { get; private set; } = [];
    public record ProfileMetric(string Label, int Value, string Icon, string? Suffix = null);

    public async Task<IActionResult> OnGetAsync(string? tab = null)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("jwt")))
            return RedirectToPage("/Account/Login");
        ActiveTab = tab is "edit" or "password" or "attempts" ? tab : "overview";
        UserProfile = await api.GetProfileAsync();
        if (UserProfile is null) return RedirectToPage("/Account/Login");
        FullName = UserProfile.FullName;
        Avatar = UserProfile.Avatar;
        ProgressSummary? progress = null;
        try { progress = await api.GetProgressAsync(); }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        RecentAttempts = progress?.Recent ?? [];
        if (progress is not null)
            Metrics = [new("Best score", progress.BestScore, "workspace_premium"),
                       new("Completed exams", progress.TotalAttempts, "description"),
                       new("Average score", (int)progress.AverageScore, "monitoring"),
                       new("Assigned roles", UserProfile.Roles.Count, "badge")];
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

        if (string.IsNullOrWhiteSpace(FullName) || FullName.Trim().Length is < 2 or > 100)
        {
            var msg = "Full name must be between 2 and 100 characters.";
            if (isAjax) return new JsonResult(new { error = msg }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = msg;
            return RedirectToPage(new { tab = "edit" });
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

        return RedirectToPage(new { tab = "edit" });
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
            return RedirectToPage(new { tab = "password" });
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            var msg = "New password must be at least 6 characters.";
            if (isAjax) return new JsonResult(new { error = msg }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = msg;
            return RedirectToPage(new { tab = "password" });
        }

        if (NewPassword != ConfirmPassword)
        {
            var msg = "Passwords do not match.";
            if (isAjax) return new JsonResult(new { error = msg }) { StatusCode = StatusCodes.Status400BadRequest };
            ErrorMessage = msg;
            return RedirectToPage(new { tab = "password" });
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

        return RedirectToPage(new { tab = "password" });
    }
}
