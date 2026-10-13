using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Vocabulary;

public class DictionarySearchModel(ToeicApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    public bool HasSearched { get; set; }
    public VocabularyDetailDto? Vocabulary { get; set; }
    public bool IsSavedInNotebook { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Keyword))
        {
            return Page();
        }

        HasSearched = true;
        Vocabulary = await api.LookupVocabularyAsync(Keyword);

        if (Vocabulary != null)
        {
            // Check if already in notebook
            var notebook = await api.GetSavedVocabulariesAsync(Vocabulary.Word, 1, 10);
            IsSavedInNotebook = notebook?.Items.Any(x => x.VocabularyId == Vocabulary.VocabularyId) ?? false;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(int vocabularyId)
    {
        var (success, error) = await api.SaveVocabularyAsync(vocabularyId);
        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Không thể lưu từ vào sổ tay.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã lưu từ vựng vào sổ tay cá nhân thành công!";
        }

        return RedirectToPage(new { Keyword });
    }
}
