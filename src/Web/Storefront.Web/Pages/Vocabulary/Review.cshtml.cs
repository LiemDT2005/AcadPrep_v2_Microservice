using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Vocabulary;

public class ReviewModel(ToeicApiClient api) : PageModel
{
    public PaginatedListDto<SavedVocabularyDto> DueFlashcards { get; set; } = new([], 1, 0, 0, false, false);
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
    {
        var result = await api.GetReviewFlashcardsAsync(pageNumber, 10);
        if (result != null)
        {
            DueFlashcards = result;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRateAsync(int vocabularyId, bool isRemembered)
    {
        var (success, error) = await api.RateVocabularyAsync(vocabularyId, isRemembered);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Không thể đánh giá từ vựng.";
        }
        else
        {
            TempData["SuccessMessage"] = isRemembered 
                ? "Tuyệt vời! Thuật toán SRS đã kéo dài thời gian ôn tiếp theo (x2 interval)." 
                : "Đã ghi nhận! Từ này sẽ được đưa vào danh sách ôn lại vào ngày mai.";
        }

        return RedirectToPage();
    }
}
