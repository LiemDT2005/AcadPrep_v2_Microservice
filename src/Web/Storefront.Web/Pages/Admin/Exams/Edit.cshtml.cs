using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams;

public class EditModel(ToeicApiClient api) : PageModel
{
    public AdminExamDetail Exam { get; set; } = null!;
    public List<AdminExamSeries> ExamSeries { get; set; } = [];
    public List<string> UpdateErrors { get; set; } = [];

    [BindProperty]
    public UpdateExamFormModel UpdateForm { get; set; } = new();

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

        var req = new AdminUpdateExamRequest(
            UpdateForm.Title.Trim(),
            UpdateForm.ExamType ?? Exam.ExamType ?? "Full Test",
            UpdateForm.Description?.Trim(),
            UpdateForm.Duration,
            UpdateForm.ExamSeriesId,
            UpdateForm.AudioUrl?.Trim(),
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
}
