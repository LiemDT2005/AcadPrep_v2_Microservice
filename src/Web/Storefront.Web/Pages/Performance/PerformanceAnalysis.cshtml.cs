using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Performance;

public class PerformanceAnalysisModel(ToeicApiClient api) : PageModel
{
    public PerformanceAnalysisDto AnalysisData { get; set; } = new(false, "Chưa có dữ liệu phân tích.", 0, 0, 0, new Dictionary<int, int>());

    public async Task<IActionResult> OnGetAsync([FromQuery] string? userId)
    {
        var targetUser = userId ?? HttpContext.Session.GetString("userId");
        var result = await api.GetPerformanceAnalysisAsync(targetUser);
        if (result != null)
        {
            AnalysisData = result;
        }

        return Page();
    }
}
