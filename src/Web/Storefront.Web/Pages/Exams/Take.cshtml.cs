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
        if (session?.IsSubmitted == true)
            return RedirectToPage("/Exams/Results", new { attemptId = mode == "full-test" ? (int?)id : null, sessionId = mode == "practice" ? (int?)id : null });
        TestSession = session;
        return Page();
    }
    public async Task<IActionResult> OnPostCheckpointAsync(int id, string mode, [FromBody] SaveSessionRequest request)
    {
        if (mode is not ("full-test" or "practice") || !ModelState.IsValid) return BadRequest();
        var result = await api.SaveSessionAsync(id, mode, request);
        return new JsonResult(new { saved = result.Success, error = result.Error }) { StatusCode = result.Success ? 200 : 400 };
    }
    public async Task<IActionResult> OnPostSubmitAsync(int id, string mode, [FromBody] SaveSessionRequest request)
    {
        if (mode is not ("full-test" or "practice") || !ModelState.IsValid) return BadRequest();
        var result = await api.SubmitSessionAsync(id, mode, request);
        if (!result.Success) return new JsonResult(new { error = result.Error }) { StatusCode = 400 };
        return new JsonResult(new { redirectUrl = Url.Page("/Exams/Results", new { attemptId = mode == "full-test" ? (int?)id : null, sessionId = mode == "practice" ? (int?)id : null }) });
    }
}
