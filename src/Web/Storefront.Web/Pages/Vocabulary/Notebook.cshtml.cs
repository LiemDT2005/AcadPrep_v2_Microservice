using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Vocabulary;

public class NotebookModel(ToeicApiClient api) : PageModel
{
    public PaginatedListDto<SavedVocabularyDto> SavedVocabularies { get; set; } = new([], 1, 0, 0, false, false);

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await api.GetSavedVocabulariesAsync(SearchTerm, PageNumber, 10);
        if (result != null)
        {
            SavedVocabularies = result;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int vocabularyId)
    {
        var (success, error) = await api.RemoveSavedVocabularyAsync(vocabularyId);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Không thể xóa từ khỏi sổ tay.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã xóa từ vựng khỏi sổ tay thành công.";
        }

        return RedirectToPage(new { PageNumber, SearchTerm });
    }
}
