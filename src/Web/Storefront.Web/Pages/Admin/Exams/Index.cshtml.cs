using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams;

public class IndexModel(ToeicApiClient api) : PageModel
{
    public List<AdminExamListItem> Exams { get; set; } = [];
    public List<AdminExamSeries> ExamSeries { get; set; } = [];

    public int TotalExams { get; set; }
    public int ActiveExams { get; set; }
    public int TotalAttempts { get; set; }
    public int HiddenExams { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; } = "All";

    [BindProperty(SupportsGet = true)]
    public string? ExamType { get; set; } = "All";

    public bool ShowCreateModal { get; set; }
    public List<string> CreateExamErrors { get; set; } = [];

    [BindProperty]
    public CreateExamFormModel CreateExamForm { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int page = 1)
    {
        PageNumber = Math.Max(1, page);
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        ShowCreateModal = true;
        await LoadMetadataAsync();

        if (string.IsNullOrWhiteSpace(CreateExamForm.Title))
        {
            CreateExamErrors.Add("Exam title is required.");
        }

        if (CreateExamForm.Duration <= 0)
        {
            CreateExamErrors.Add("Exam duration must be greater than 0 minutes.");
        }

        if (CreateExamForm.ExamSeriesId <= 0)
        {
            CreateExamErrors.Add("Please select an exam series.");
        }

        if (CreateExamErrors.Count > 0)
        {
            await LoadPageDataAsync();
            return Page();
        }

        var request = new AdminCreateExamRequest(
            CreateExamForm.Title.Trim(),
            CreateExamForm.ExamType ?? "Full Test",
            CreateExamForm.Description?.Trim(),
            CreateExamForm.Duration,
            CreateExamForm.ExamSeriesId,
            CreateExamForm.AudioUrl?.Trim()
        );

        var (createdId, error) = await api.CreateAdminExamAsync(request);
        if (createdId.HasValue)
        {
            TempData["SuccessMessage"] = $"Exam \"{CreateExamForm.Title}\" created successfully.";
            return RedirectToPage("./Edit", new { id = createdId.Value });
        }

        CreateExamErrors.Add(error ?? "Failed to create exam.");
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var (result, error) = await api.DeleteAdminExamAsync(id);
        if (result != null)
        {
            return new JsonResult(new { success = true, message = result.Message, softDeleted = result.SoftDeleted });
        }
        return new JsonResult(new { success = false, error = error ?? "Failed to delete exam." });
    }

    public async Task<IActionResult> OnPostRestoreAsync(int id)
    {
        var (success, error) = await api.RestoreAdminExamAsync(id);
        if (success)
        {
            return new JsonResult(new { success = true, message = "Exam restored and published successfully." });
        }
        return new JsonResult(new { success = false, error = error ?? "Failed to restore exam." });
    }

    private async Task LoadPageDataAsync()
    {
        var paged = await api.GetAdminExamsPagedAsync(Keyword, Status, ExamType, PageNumber, PageSize);
        if (paged != null)
        {
            Exams = paged.Items;
            TotalCount = paged.TotalCount;
            TotalPages = paged.TotalPages;
            PageNumber = paged.Page;
            PageSize = paged.PageSize;
        }

        await LoadMetadataAsync();
    }

    private async Task LoadMetadataAsync()
    {
        ExamSeries = await api.GetExamSeriesAsync();

        // Calculate summary counts from all exams if available or approximate
        var allExamsResult = await api.GetAdminExamsPagedAsync(null, "All", null, 1, 1000);
        if (allExamsResult != null)
        {
            var all = allExamsResult.Items;
            TotalExams = all.Count;
            ActiveExams = all.Count(e => !e.IsDeleted && e.Status == "Published");
            HiddenExams = all.Count(e => e.IsDeleted || e.Status == "Hidden");
            TotalAttempts = all.Sum(e => e.AttemptCount);
        }
    }
}

public class CreateExamFormModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ExamType { get; set; } = "Full Test";
    public int Duration { get; set; } = 120;
    public int ExamSeriesId { get; set; }
    public string? AudioUrl { get; set; }
}
