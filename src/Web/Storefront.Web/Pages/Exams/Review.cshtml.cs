using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;
namespace Storefront.Web.Pages.Exams;
public class ReviewModel(ToeicApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? AttemptId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SessionId { get; set; }
    [BindProperty(SupportsGet = true)] public string Filter { get; set; } = "all";
    public AttemptReviewDto Review { get; set; } = new();
    public async Task<IActionResult> OnGetAsync()
    {
        if (AttemptId.HasValue == SessionId.HasValue) return BadRequest();
        var review = await api.GetSessionReviewAsync(SessionId ?? AttemptId!.Value, SessionId.HasValue ? "practice" : "full-test");
        if (review is null) return NotFound("Submitted session unavailable.");
        Review = review;
        return Page();
    }
}
