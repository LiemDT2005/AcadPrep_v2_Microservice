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
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("jwt"))) return RedirectToPage("/Account/Login");
        try { Stats = await api.GetExamStatsAsync(pageNumber, pageSize); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (HttpRequestException) { return StatusCode(503, "Exam statistics unavailable."); }
        catch (TaskCanceledException) { return StatusCode(503, "Exam statistics timed out."); }
        return Page();
    }
}
