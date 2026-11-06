using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Pages.Admin.Exams.Questions;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams;

public class EditModel(ToeicApiClient api, IWebHostEnvironment env) : PageModel
{
    public AdminExamDetail Exam { get; set; } = null!;
    public List<AdminExamSeries> ExamSeries { get; set; } = [];
    public List<AdminPassageListItem> ExamPassages { get; set; } = [];
    public List<string> UpdateErrors { get; set; } = [];

    [BindProperty]
    public UpdateExamFormModel UpdateForm { get; set; } = new();

    [BindProperty]
    public QuickCreatePassageFormModel CreatePassageForm { get; set; } = new();

    [BindProperty]
    public QuickAddQuestionFormModel QuickQuestionForm { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadExamAsync(id))
        {
            return RedirectToPage("./Index");
        }

        if (Exam.IsDeleted || Exam.Status == "Hidden")
        {
            return RedirectToPage("./Detail", new { id });
        }

        BindUpdateForm();
        await LoadExamSeriesAsync();
        await LoadExamPassagesAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        if (id <= 0)
        {
            id = UpdateForm.Id;
        }

        if (id <= 0 || !await LoadExamAsync(id))
        {
            ErrorMessage = "Exam not found or could not be updated.";
            return RedirectToPage("./Index");
        }

        await LoadExamSeriesAsync();
        await LoadExamPassagesAsync(id);
        UpdateForm.Id = id;

        if (string.IsNullOrWhiteSpace(UpdateForm.Title))
        {
            UpdateErrors.Add("Exam title is required.");
        }

        if (UpdateForm.Duration <= 0 || UpdateForm.Duration > 240)
        {
            UpdateErrors.Add("Exam duration must be between 1 and 240 minutes.");
        }

        if (UpdateForm.ExamSeriesId <= 0)
        {
            UpdateErrors.Add("Please select an exam series.");
        }

        if (UpdateErrors.Count > 0)
        {
            return Page();
        }

        var audioUrl = UpdateForm.AudioUrl?.Trim();
        if (UpdateForm.AudioFile != null && UpdateForm.AudioFile.Length > 0)
        {
            var uploadedUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, UpdateForm.AudioFile, "audio");
            if (!string.IsNullOrWhiteSpace(uploadedUrl))
            {
                audioUrl = uploadedUrl;
            }
        }

        var req = new AdminUpdateExamRequest(
            UpdateForm.Title.Trim(),
            UpdateForm.ExamType ?? Exam.ExamType ?? "Full Test",
            UpdateForm.Description?.Trim(),
            UpdateForm.Duration,
            UpdateForm.ExamSeriesId,
            audioUrl,
            Exam.Status
        );

        var (success, error) = await api.UpdateAdminExamAsync(id, req);
        if (success)
        {
            SuccessMessage = "Exam information updated successfully.";
            return RedirectToPage("./Edit", new { id });
        }

        UpdateErrors.Add(error ?? "Failed to update exam.");
        return Page();
    }

    public async Task<IActionResult> OnPostChangeStatusAsync(int id, string status)
    {
        var (success, error) = await api.ChangeExamStatusAsync(id, status);
        if (success)
        {
            SuccessMessage = $"Exam status changed to {status} successfully.";
        }
        else
        {
            ErrorMessage = error ?? "Failed to change exam status.";
        }
        return RedirectToPage("./Edit", new { id });
    }

    public async Task<IActionResult> OnPostCreatePassageAsync(int id)
    {
        if (string.IsNullOrWhiteSpace(CreatePassageForm.Content) && string.IsNullOrWhiteSpace(CreatePassageForm.ImageUrl))
        {
            ErrorMessage = "Passage content or image URL is required.";
            return RedirectToPage("./Edit", new { id });
        }

        var req = new AdminUpsertPassageRequest(
            id,
            CreatePassageForm.Content?.Trim(),
            CreatePassageForm.ImageUrl?.Trim(),
            CreatePassageForm.Explanation?.Trim(),
            Math.Max(1, CreatePassageForm.DisplayOrder),
            null
        );

        var (passageId, error) = await api.CreateAdminPassageAsync(req);
        if (passageId.HasValue)
        {
            SuccessMessage = $"Created Reading Passage #{passageId.Value} in this exam successfully.";
        }
        else
        {
            ErrorMessage = error ?? "Failed to create reading passage.";
        }

        return RedirectToPage("./Edit", new { id });
    }

    public async Task<IActionResult> OnPostDeletePassageAsync(int id, int passageId, bool deleteWithQuestions = false)
    {
        var (res, error) = await api.DeleteAdminPassageAsync(passageId, deleteWithQuestions);
        if (res != null)
        {
            SuccessMessage = res.Message;
        }
        else
        {
            ErrorMessage = error ?? "Cannot delete passage while questions are attached (UC-18.5 Non-Orphan Constraint).";
        }

        return RedirectToPage("./Edit", new { id });
    }

    public async Task<IActionResult> OnPostQuickAddQuestionAsync(int id)
    {
        var exam = await api.GetAdminExamAsync(id);
        if (exam != null && !exam.IsDeleted)
        {
            var part = Math.Clamp(QuickQuestionForm.Part, 1, 7);
            var partCount = exam.Questions.Count(q => q.Part == part);
            var limit = part switch
            {
                1 => ExamStructureViewModel.TargetP1,
                2 => ExamStructureViewModel.TargetP2,
                3 => ExamStructureViewModel.TargetP3,
                4 => ExamStructureViewModel.TargetP4,
                5 => ExamStructureViewModel.TargetP5,
                6 => ExamStructureViewModel.TargetP6,
                7 => ExamStructureViewModel.TargetP7,
                _ => 200
            };

            if (partCount >= limit)
            {
                ErrorMessage = $"Part {part} already has the maximum of {limit} questions.";
                return RedirectToPage("./Edit", new { id });
            }
        }

        string? audioUrl = null;
        if (QuickQuestionForm.AudioFile != null && QuickQuestionForm.AudioFile.Length > 0)
        {
            audioUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, QuickQuestionForm.AudioFile, "audio");
        }

        var req = new AdminUpsertQuestionRequest(
            id,
            Math.Clamp(QuickQuestionForm.Part, 1, 7),
            Math.Max(1, QuickQuestionForm.QuestionNumber),
            QuickQuestionForm.QuestionText?.Trim(),
            QuickQuestionForm.CorrectOption ?? "A",
            QuickQuestionForm.Explanation?.Trim(),
            audioUrl,
            null,
            null,
            null,
            QuickQuestionForm.QuestionType?.Trim(),
            QuickQuestionForm.TopicTag?.Trim(),
            QuickQuestionForm.Difficulty ?? "Medium",
            QuickQuestionForm.PassageId > 0 ? QuickQuestionForm.PassageId : null,
            null,
            QuickQuestionForm.OptionA?.Trim() ?? "Option A",
            QuickQuestionForm.OptionB?.Trim() ?? "Option B",
            QuickQuestionForm.OptionC?.Trim() ?? "Option C",
            QuickQuestionForm.Part != 2 ? (QuickQuestionForm.OptionD?.Trim() ?? "Option D") : null
        );

        var (qId, error) = await api.CreateAdminQuestionAsync(req);
        if (qId.HasValue)
        {
            SuccessMessage = $"Added Question #{QuickQuestionForm.QuestionNumber} (Part {QuickQuestionForm.Part}) to this exam.";
        }
        else
        {
            ErrorMessage = error ?? "Failed to create question.";
        }

        return RedirectToPage("./Edit", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string contentType, int targetId)
    {
        if (id <= 0 || targetId <= 0 || string.IsNullOrWhiteSpace(contentType))
        {
            ErrorMessage = "Invalid content deletion request.";
            return RedirectToPage("./Edit", new { id });
        }

        var (res, error) = await api.DeleteExamContentAsync(id, contentType, targetId);
        if (res != null)
        {
            SuccessMessage = res.Message;
        }
        else
        {
            ErrorMessage = error ?? "Failed to delete exam content.";
        }

        return RedirectToPage("./Edit", new { id });
    }

    private async Task<bool> LoadExamAsync(int id)
    {
        var exam = await api.GetAdminExamAsync(id);
        if (exam == null) return false;
        Exam = exam;
        return true;
    }

    private void BindUpdateForm()
    {
        UpdateForm = new UpdateExamFormModel
        {
            Id = Exam.Id,
            Title = Exam.Title,
            ExamType = Exam.ExamType,
            Description = Exam.Description,
            Duration = Exam.Duration,
            ExamSeriesId = Exam.ExamSeriesId,
            AudioUrl = Exam.AudioUrl
        };
    }

    private async Task LoadExamSeriesAsync()
    {
        ExamSeries = await api.GetExamSeriesAsync();
    }

    private async Task LoadExamPassagesAsync(int examId)
    {
        var paged = await api.GetAdminPassagesPagedAsync(examId: examId, page: 1, pageSize: 100);
        ExamPassages = paged?.Items?.ToList() ?? [];
    }
}

public class UpdateExamFormModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ExamType { get; set; } = "Full Test";
    public string? Description { get; set; }
    public int Duration { get; set; } = 120;
    public int ExamSeriesId { get; set; }
    public string? AudioUrl { get; set; }
    public IFormFile? AudioFile { get; set; }
}

public class QuickCreatePassageFormModel
{
    public int DisplayOrder { get; set; } = 1;
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public string? Explanation { get; set; }
}

public class QuickAddQuestionFormModel
{
    public int Part { get; set; } = 5;
    public int QuestionNumber { get; set; } = 101;
    public string Difficulty { get; set; } = "Medium";
    public string? QuestionType { get; set; } = "Grammar";
    public string? TopicTag { get; set; }
    public int? PassageId { get; set; }
    public string? QuestionText { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string CorrectOption { get; set; } = "A";
    public string? Explanation { get; set; }
    public IFormFile? AudioFile { get; set; }
}
