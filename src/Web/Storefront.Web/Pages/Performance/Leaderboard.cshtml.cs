using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Performance;

public class LeaderboardModel(ToeicApiClient api) : PageModel
{
    public LeaderboardResultDto LeaderboardData { get; set; } = new([], null);

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "Score";

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(SortBy) ||
            (!string.Equals(SortBy, "Score", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(SortBy, "Streak", StringComparison.OrdinalIgnoreCase)))
        {
            SortBy = "Score";
        }

        var result = await api.GetLeaderboardAsync(SortBy);
        if (result != null)
        {
            LeaderboardData = result;
        }

        return Page();
    }
}
