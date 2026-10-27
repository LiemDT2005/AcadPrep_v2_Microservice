using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Vocabulary;

public class VocabularyDetailModel(ToeicApiClient api) : PageModel
{
    public VocabContextDto? Context { get; set; }

    public async Task<IActionResult> OnGetAsync(int vocabularyId)
    {
        Context = await api.GetVocabularyPassagesAsync(vocabularyId);
        if (Context == null)
        {
            return NotFound();
        }

        return Page();
    }
}
