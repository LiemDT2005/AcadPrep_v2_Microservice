using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Users;

public class IndexModel(ToeicApiClient api) : PageModel
{
    public UserListResponse? UserList { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Role { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public string CurrentAdminId => HttpContext.Session.GetString("userId") ?? string.Empty;
    public string[] AvailableRoles => ["Admin", "Moderator", "Learner"];

    private bool IsAdmin()
    {
        var roles = HttpContext.Session.GetString("roles") ?? string.Empty;
        return roles.Split(',').Contains("Admin");
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsAdmin())
        {
            return RedirectToPage("/Account/Login");
        }

        PageIndex = Math.Max(1, PageIndex);
        UserList = await api.GetUsersAsync(Search, Role == "all" ? null : Role, Status == "all" ? null : Status, PageIndex, 10);
        if (UserList is null) ErrorMessage = "Unable to load accounts. Please try again.";
        return Page();
    }

    public async Task<IActionResult> OnPostAssignRolesAsync(string userId, List<string> selectedRoles)
    {
        if (!IsAdmin())
            return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(userId))
        {
            ErrorMessage = "User identifier not found.";
            return RedirectToPage(new { Search, Role, Status, PageIndex });
        }

        if (selectedRoles is null || selectedRoles.Count == 0 || selectedRoles.Except(AvailableRoles).Any())
        {
            ErrorMessage = "User must have at least one assigned role.";
            return RedirectToPage(new { Search, Role, Status, PageIndex });
        }

        var (success, error) = await api.AssignRolesAsync(userId, selectedRoles);
        if (!success)
        {
            ErrorMessage = error ?? "Failed to assign roles.";
        }
        else
        {
            SuccessMessage = "Roles updated successfully!";
        }

        return RedirectToPage(new { Search, Role, Status, PageIndex });
    }

    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        if (!IsAdmin())
            return RedirectToPage("/Account/Login");

        var (bytes, fileName) = await api.DownloadUsersCsvAsync();
        if (bytes is null || bytes.Length == 0)
        {
            ErrorMessage = "Unable to export CSV file at this time.";
            return RedirectToPage(new { Search, Role, Status, PageIndex });
        }

        return File(bytes, "text/csv; charset=utf-8", fileName ?? "users.csv");
    }

    public async Task<IActionResult> OnGetDetailJsonAsync(string id)
    {
        if (!IsAdmin())
            return Unauthorized();

        var detail = await api.GetUserDetailAsync(id);
        return new JsonResult(detail);
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(string userId, int targetStatus, string? banReason)
    {
        if (!IsAdmin())
            return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(userId))
        {
            ErrorMessage = "User not found.";
            return RedirectToPage(new { Search, Role, Status, PageIndex });
        }

        if (targetStatus is not (1 or 2)) return BadRequest();

        var (success, error) = await api.UpdateUserStatusAsync(userId, targetStatus, banReason);
        if (!success)
        {
            ErrorMessage = error ?? "Failed to update account status.";
        }
        else
        {
            SuccessMessage = targetStatus == 2 
                ? "Account banned and real-time Force Logout dispatched!" 
                : "Account unbanned successfully!";
        }

        return RedirectToPage(new { Search, Role, Status, PageIndex });
    }
}
