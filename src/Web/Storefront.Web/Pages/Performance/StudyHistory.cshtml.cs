using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Performance;

public class StudyHistoryModel(ToeicApiClient api) : PageModel
{
    public StudyHistoryResultDto History { get; set; } = new();
    public async Task<IActionResult> OnGetAsync()
    {
        var data = await api.GetStudyHistoryAsync();
        if (data is null) return StatusCode(503, "Unable to load study data. Please try again.");
        History = data;
        return Page();
    }
}
