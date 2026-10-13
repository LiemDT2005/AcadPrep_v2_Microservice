using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Account;

public class ProfileModel(ToeicApiClient api) : PageModel
{
    public UserProfileDto? UserProfile { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
        {
            return RedirectToPage("/Account/Login");
        }

        UserProfile = await api.GetProfileAsync();
        if (UserProfile is null)
        {
            var userId = HttpContext.Session.GetString("userId") ?? "N/A";
            var name = HttpContext.Session.GetString("name") ?? "User";
            var avatar = HttpContext.Session.GetString("avatar");
            UserProfile = new UserProfileDto(
                Id: userId,
                Email: "Đang tải...",
                FullName: name,
                Avatar: avatar,
                Status: "Active",
                Roles: ["Learner"],
                CreatedAt: DateTime.UtcNow.AddHours(7),
                LastLoginAt: DateTime.UtcNow.AddHours(7)
            );
        }

        return Page();
    }
}
