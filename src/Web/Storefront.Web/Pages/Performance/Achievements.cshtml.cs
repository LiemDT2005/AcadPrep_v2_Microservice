using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Performance;

public class AchievementsModel(ToeicApiClient api) : PageModel
{
    public List<AchievementDto> Achievements { get; set; } = [];
    public StudyStreakDto? Streak { get; set; }

    public async Task<IActionResult> OnGetAsync([FromQuery] string? userId)
    {
        var targetUser = userId ?? HttpContext.Session.GetString("userId");
        Achievements = await api.GetAchievementsAsync(targetUser);
        Streak = await api.GetStudyStreakAsync(targetUser);

        return Page();
    }
}
