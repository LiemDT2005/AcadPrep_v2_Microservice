using Microsoft.AspNetCore.Mvc;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin;

// Recipient picker for Huy's admin forms (notification to one user, manual plan grant): the admin types an email or a
// name and picks an account; the form still submits the account GUID, so existing FE/BE validation is unchanged.
// Uses Identity's existing Admin API (GET /api/users?search=) with the admin's own JWT.
public static class AdminUserLookup
{
    public const int MaxResults = 8;

    public static async Task<IActionResult> SearchAsync(ToeicApiClient identity, string? query, string? role, string? status)
    {
        var text = query?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 2 || text.Length > 100)
            return new JsonResult(new { items = Array.Empty<object>() });
        var users = await identity.GetUsersAsync(text, role, status, 1, MaxResults);
        if (users is null)
            return new JsonResult(new { message = "Không thể tìm người dùng lúc này." }) { StatusCode = 502 };
        return new JsonResult(new
        {
            items = users.Items.Select(user => new { user.Id, user.Email, user.FullName, user.Status, user.Roles })
        });
    }
}
