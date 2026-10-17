using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Exams;

[IgnoreAntiforgeryToken]
public class DetailModel(ToeicApiClient apiClient) : PageModel
{
    public GetExamDetailDto? ExamDetail { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsLoggedIn { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userIdStr = HttpContext.Session.GetString("userId");
        int? parsedUserId = null;
        if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var uId))
        {
            parsedUserId = uId;
            IsLoggedIn = true;
        }
        else
        {
            IsLoggedIn = !string.IsNullOrEmpty(HttpContext.Session.GetString("jwt"));
        }

        ExamDetail = await apiClient.GetExamDetailAsync(id, parsedUserId);

        if (ExamDetail is null)
        {
            ErrorMessage = "Exam details not found or have been deleted.";
            TempData["ErrorMessage"] = ErrorMessage;
            return RedirectToPage("/Exams/Index");
        }

        return Page();
    }
}
