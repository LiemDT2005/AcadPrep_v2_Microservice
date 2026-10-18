using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Exams;

public class DetailModel(ToeicApiClient apiClient) : PageModel
{
    public GetExamDetailDto? ExamDetail { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(HttpContext.Session.GetString("jwt"));

    public async Task<IActionResult> OnGetAsync(int id)
    {
        ExamDetail = await apiClient.GetExamDetailAsync(id);

        if (ExamDetail is null)
        {
            ErrorMessage = "Exam details not found or have been deleted.";
            TempData["ErrorMessage"] = ErrorMessage;
            return RedirectToPage("/Exams/Index");
        }

        return Page();
    }

    public Task<IActionResult> OnPostStartFullTestAsync(int id) => StartAsync(id, null);

    public Task<IActionResult> OnPostStartPracticeAsync(int id, [FromBody] StartPracticeRequest? request)
    {
        if (request is null || !ModelState.IsValid)
            return Task.FromResult<IActionResult>(new JsonResult(new { error = "Invalid practice selection." }) { StatusCode = 400 });
        return StartAsync(id, request);
    }

    private async Task<IActionResult> StartAsync(int id, StartPracticeRequest? practice)
    {
        var result = await apiClient.StartSessionAsync(id, practice);
        if (result.Unauthorized)
            return new JsonResult(new { error = result.Error }) { StatusCode = 401 };
        if (result.SessionId is null)
            return new JsonResult(new { error = result.Error }) { StatusCode = 400 };
        return new JsonResult(new
        {
            redirectUrl = Url.Page("/Exams/Take", new { id = result.SessionId, mode = practice is null ? "full-test" : "practice" })
        });
    }
}
