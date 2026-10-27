using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Services;
namespace Storefront.Web.Pages.Exams;
public class ResultsModel(ToeicApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? AttemptId { get; set; }
    [BindProperty(SupportsGet = true)] public int? SessionId { get; set; }
    public bool IsPractice { get; set; }
    public int TotalScore { get; set; }
    public int MaxScore { get; set; }
    public int ListeningScore { get; set; }
    public int ReadingScore { get; set; }
    public int ListeningMax { get; set; }
    public int ReadingMax { get; set; }
    public int ExamId { get; set; }
    public async Task<IActionResult> OnGetAsync()
    {
        if (AttemptId.HasValue == SessionId.HasValue) return BadRequest();
        var r = await api.GetSessionReviewAsync(SessionId ?? AttemptId!.Value, SessionId.HasValue ? "practice" : "full-test");
        if (r is null) return NotFound("Submitted session unavailable.");
        IsPractice = r.IsPractice; TotalScore = r.TotalScore; MaxScore = r.MaxScore;
        ListeningScore = r.ListeningScore; ReadingScore = r.ReadingScore; ExamId = r.ExamId;
        ListeningMax = IsPractice ? r.Questions.Count(q => q.Part <= 4) : 495;
        ReadingMax = IsPractice ? r.Questions.Count(q => q.Part >= 5) : 495;
        return Page();
    }
}
