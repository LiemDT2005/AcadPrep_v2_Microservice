using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin;

public class ExamStatsModel(ToeicApiClient api) : PageModel
{
    public PaginatedListDto<ExamStatsItemDto>? Stats { get; set; }

    public async Task<IActionResult> OnGetAsync(int pageNumber = 1, int pageSize = 10)
    {
        Stats = await api.GetExamStatsAsync(pageNumber, pageSize);
        return Page();
    }
}
