using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Passages;

public class DetailModel(ToeicApiClient api) : PageModel
{
    public AdminPassageCmsDetail? Passage { get; set; }
    public List<AdminExamListItem> AvailableExams { get; set; } = [];
    public List<string> FormErrors { get; set; } = [];

    [BindProperty]
    public UpsertPassageFormModel EditForm { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var passageTask = api.GetAdminPassageDetailAsync(id);
        var examsTask = api.GetAdminExamsPagedAsync(null, "All", "All", 1, 100);

        await Task.WhenAll(passageTask, examsTask);

        Passage = await passageTask;
        if (Passage == null)
            return RedirectToPage("./Index");

        var exams = await examsTask;
        if (exams != null)
            AvailableExams = exams.Items;

        EditForm = new UpsertPassageFormModel
        {
            ExamId = Passage.ExamId,
            Content = Passage.Content,
            ImageUrl = Passage.ImageUrl,
            Explanation = Passage.Explanation,
            DisplayOrder = Passage.DisplayOrder
        };

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        if (EditForm.ExamId <= 0)
            FormErrors.Add("Please select a target exam.");

        if (string.IsNullOrWhiteSpace(EditForm.Content) && string.IsNullOrWhiteSpace(EditForm.ImageUrl))
            FormErrors.Add("Passage content or image URL is required.");

        var current = await api.GetAdminPassageDetailAsync(id);
        if (current == null)
            return RedirectToPage("./Index");

        if (FormErrors.Count > 0)
        {
            Passage = current;
            var exams = await api.GetAdminExamsPagedAsync(null, "All", "All", 1, 100);
            if (exams != null) AvailableExams = exams.Items;
            return Page();
        }

        var req = new AdminUpsertPassageRequest(
            EditForm.ExamId,
            EditForm.Content?.Trim(),
            EditForm.ImageUrl?.Trim(),
            EditForm.Explanation?.Trim(),
            EditForm.DisplayOrder > 0 ? EditForm.DisplayOrder : 1,
            current.QuestionGroupId
        );

        var (success, error) = await api.UpdateAdminPassageAsync(id, req);
        if (success)
        {
            TempData["SuccessMessage"] = $"Passage #{id} updated successfully.";
            return RedirectToPage("./Detail", new { id });
        }

        FormErrors.Add(error ?? "Failed to update passage.");
        Passage = current;
        var examsList = await api.GetAdminExamsPagedAsync(null, "All", "All", 1, 100);
        if (examsList != null) AvailableExams = examsList.Items;
        return Page();
    }
}
