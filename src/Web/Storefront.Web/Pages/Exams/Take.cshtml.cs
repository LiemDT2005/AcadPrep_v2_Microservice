using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Exams;

public class TakeModel(ToeicApiClient api) : PageModel
{
    public ExamSessionDto? TestSession { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, string mode = "full-test")
    {
        if (mode is not ("full-test" or "practice")) return BadRequest();
        var (session, unauthorized) = await api.GetSessionAsync(id, mode);
        if (unauthorized) return StatusCode(StatusCodes.Status401Unauthorized);
        TestSession = session;
        return Page();
    }
}
