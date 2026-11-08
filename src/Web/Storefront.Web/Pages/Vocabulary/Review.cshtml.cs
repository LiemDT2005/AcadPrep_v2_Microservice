using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Vocabulary;

public class ReviewModel(ToeicApiClient api) : PageModel
{
    public PaginatedListDto<SavedVocabularyDto> DueFlashcards { get; set; } = new([], 1, 0, 0, false, false);
    public string? Message { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "all";

    public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
    {
        bool all = !string.Equals(Mode, "due", StringComparison.OrdinalIgnoreCase);
        var result = await api.GetReviewFlashcardsAsync(pageNumber, 200, all);
        if (result != null)
        {
            DueFlashcards = result;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRateAsync(int vocabularyId, bool isRemembered, string? mode = null)
    {
        var (success, error) = await api.RateVocabularyAsync(vocabularyId, isRemembered);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Unable to submit your vocabulary review.";
        }
        else
        {
            TempData["SuccessMessage"] = isRemembered 
                ? "Tuyệt vời! Thẻ từ vựng đã được ôn tập thành công."
                : "Đã ghi nhận! Bạn có thể tiếp tục ôn lại thẻ này bất cứ lúc nào.";
        }

        return RedirectToPage(new { mode = mode ?? Mode });
    }

    public async Task<IActionResult> OnPostRateAjaxAsync([FromBody] RateAjaxRequest request)
    {
        if (request is null) return BadRequest(new { error = "Invalid request." });
        var (success, error) = await api.RateVocabularyAsync(request.VocabularyId, request.IsRemembered);
        return new JsonResult(new { success, error });
    }
}

public class RateAjaxRequest
{
    public int VocabularyId { get; set; }
    public bool IsRemembered { get; set; }
}
