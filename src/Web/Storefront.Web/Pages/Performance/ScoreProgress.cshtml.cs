using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Performance;

public class ScoreProgressModel(ToeicApiClient api) : PageModel
{
    public ScoreProgressResultDto ProgressData { get; set; } = new(false, "Chưa đủ dữ liệu để vẽ biểu đồ tiến độ.", []);

    public async Task<IActionResult> OnGetAsync([FromQuery] string? userId)
    {
        var targetUser = userId ?? HttpContext.Session.GetString("userId");
        var result = await api.GetScoreProgressAsync(targetUser);
        if (result != null)
        {
            ProgressData = result;
        }

        return Page();
    }
}
