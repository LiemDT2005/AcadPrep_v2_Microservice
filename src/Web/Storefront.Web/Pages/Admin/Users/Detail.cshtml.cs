using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Users;

public class DetailModel(ToeicApiClient api) : PageModel
{
    public UserDetailAdminDto? Account { get; private set; }
    public string[] AvailableRoles => ["Admin", "Moderator", "Learner"];
    public string CurrentAdminId => HttpContext.Session.GetString("userId") ?? string.Empty;
    private bool IsAdmin => (HttpContext.Session.GetString("roles") ?? "").Split(',').Contains("Admin");

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (!IsAdmin) return RedirectToPage("/Account/Login");
        if (string.IsNullOrWhiteSpace(id)) return NotFound();
        Account = await api.GetUserDetailAsync(id);
        return Account is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAssignRolesAsync(string id, List<string> selectedRoles)
    {
        if (!IsAdmin) return RedirectToPage("/Account/Login");
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();
        if (selectedRoles.Count == 0 || selectedRoles.Except(AvailableRoles).Any())
        {
            TempData["ErrorMessage"] = "Select at least one valid role.";
            return RedirectToPage(new { id });
        }
        var (success, error) = await api.AssignRolesAsync(id, selectedRoles);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Roles updated successfully." : error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(string id, int targetStatus, string? banReason)
    {
        if (!IsAdmin) return RedirectToPage("/Account/Login");
        if (string.IsNullOrWhiteSpace(id) || targetStatus is not (1 or 2)) return BadRequest();
        var (success, error) = await api.UpdateUserStatusAsync(id, targetStatus, banReason);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account status updated successfully." : error;
        return RedirectToPage(new { id });
    }
}
