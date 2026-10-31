using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Pages.Admin.Exams.Questions;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Questions;

public class DetailModel(ToeicApiClient api, IWebHostEnvironment env) : PageModel
{
    public AdminQuestionBankDetail Question { get; set; } = null!;
    public List<AdminExamListItem> Exams { get; set; } = [];

    [BindProperty]
    public UpsertQuestionFormModel EditForm { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var detail = await api.GetAdminQuestionDetailAsync(id);
        if (detail == null)
        {
            TempData["ErrorMessage"] = "Question not found.";
            return RedirectToPage("./Index");
        }

        Question = detail;
        var examsPaged = await api.GetAdminExamsPagedAsync(pageNumber: 1, pageSize: 100);
        Exams = examsPaged?.Items.Where(e => !e.IsDeleted).ToList() ?? [];

        BindEditForm(detail);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        var existing = await api.GetAdminQuestionDetailAsync(id);
        if (existing == null)
        {
            TempData["ErrorMessage"] = "Question not found.";
            return RedirectToPage("./Index");
        }

        string? imageUrl = EditForm.ImageUrl ?? existing.ImageUrl;
        if (EditForm.ImageFile != null && EditForm.ImageFile.Length > 0)
        {
            imageUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, EditForm.ImageFile, "images");
        }

        string? audioUrl = EditForm.AudioUrl ?? existing.AudioUrl;
        if (EditForm.AudioFile != null && EditForm.AudioFile.Length > 0)
        {
            audioUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, EditForm.AudioFile, "audio");
        }

        var req = new AdminUpsertQuestionRequest(
            EditForm.ExamId,
            EditForm.Part,
            EditForm.QuestionNumber,
            EditForm.QuestionText,
            EditForm.CorrectOption,
            EditForm.Explanation,
            audioUrl,
            EditForm.AudioStartSecond ?? existing.AudioStartSecond,
            EditForm.AudioEndSecond ?? existing.AudioEndSecond,
            imageUrl,
            EditForm.QuestionType,
            EditForm.TopicTag,
            EditForm.Difficulty,
            existing.PassageId,
            existing.QuestionGroupId,
            EditForm.OptionA,
            EditForm.OptionB,
            EditForm.OptionC,
            EditForm.Part == 2 ? null : EditForm.OptionD
        );

        var (ok, err) = await api.UpdateAdminQuestionAsync(id, req);
        if (ok)
        {
            SuccessMessage = $"Question #{EditForm.QuestionNumber} updated successfully.";
            return RedirectToPage("./Detail", new { id });
        }

        ErrorMessage = err ?? "Failed to update question.";
        return RedirectToPage("./Detail", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var (res, err) = await api.DeleteAdminQuestionAsync(id);
        if (res != null)
        {
            TempData["SuccessMessage"] = res.Message;
            return RedirectToPage("./Index");
        }

        ErrorMessage = err ?? "Failed to delete question.";
        return RedirectToPage("./Detail", new { id });
    }

    private void BindEditForm(AdminQuestionBankDetail q)
    {
        EditForm = new UpsertQuestionFormModel
        {
            QuestionId = q.Id,
            ExamId = q.ExamId,
            Part = q.Part,
            QuestionNumber = q.QuestionNumber,
            QuestionText = q.QuestionText,
            CorrectOption = q.CorrectOption,
            Explanation = q.Explanation,
            QuestionType = q.QuestionType,
            TopicTag = q.TopicTag,
            Difficulty = q.Difficulty,
            AudioUrl = q.AudioUrl,
            AudioStartSecond = q.AudioStartSecond,
            AudioEndSecond = q.AudioEndSecond,
            ImageUrl = q.ImageUrl,
            OptionA = q.Options.FirstOrDefault(o => o.OptionLetter == "A")?.OptionText ?? "",
            OptionB = q.Options.FirstOrDefault(o => o.OptionLetter == "B")?.OptionText ?? "",
            OptionC = q.Options.FirstOrDefault(o => o.OptionLetter == "C")?.OptionText ?? "",
            OptionD = q.Options.FirstOrDefault(o => o.OptionLetter == "D")?.OptionText ?? ""
        };
    }
}
