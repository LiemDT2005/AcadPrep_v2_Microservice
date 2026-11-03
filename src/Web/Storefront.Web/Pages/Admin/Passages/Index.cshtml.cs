using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Passages;

public class IndexModel(ToeicApiClient api) : PageModel
{
    public List<AdminPassageListItem> Passages { get; set; } = [];
    public List<AdminExamListItem> AvailableExams { get; set; } = [];

    public int TotalPassages { get; set; }
    public int Part6Passages { get; set; }
    public int Part7Passages { get; set; }
    public int ActiveExamPassages { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ExamId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Part { get; set; }

    [BindProperty]
    public UpsertPassageFormModel CreateForm { get; set; } = new();

    public bool ShowCreateModal { get; set; }
    public List<string> FormErrors { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int page = 1)
    {
        PageNumber = Math.Max(1, page);
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        ShowCreateModal = true;

        if (CreateForm.ExamId <= 0)
            FormErrors.Add("Please select a target exam.");

        if (string.IsNullOrWhiteSpace(CreateForm.Content) && string.IsNullOrWhiteSpace(CreateForm.ImageUrl))
            FormErrors.Add("Passage content or image URL is required.");

        if (FormErrors.Count > 0)
        {
            await LoadPageDataAsync();
            return Page();
        }

        var req = new AdminUpsertPassageRequest(
            CreateForm.ExamId,
            CreateForm.Content?.Trim(),
            CreateForm.ImageUrl?.Trim(),
            CreateForm.Explanation?.Trim(),
            CreateForm.DisplayOrder > 0 ? CreateForm.DisplayOrder : 1,
            null
        );

        var (createdId, error) = await api.CreateAdminPassageAsync(req);
        if (createdId.HasValue)
        {
            TempData["SuccessMessage"] = $"Reading passage #{createdId.Value} created successfully.";
            return RedirectToPage("./Detail", new { id = createdId.Value });
        }

        FormErrors.Add(error ?? "Failed to create reading passage.");
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, bool deleteWithQuestions = false)
    {
        var (data, error) = await api.DeleteAdminPassageAsync(id, deleteWithQuestions);
        if (data != null)
        {
            return new JsonResult(new
            {
                success = true,
                message = data.Message,
                deletedQuestionsCount = data.DeletedQuestionsCount,
                wasInActiveExam = data.WasInActiveExam,
                examTitle = data.ExamTitle
            });
        }

        return new JsonResult(new
        {
            success = false,
            error = error ?? "Cannot delete passage because it has attached Part 6-7 questions."
        });
    }

    private async Task LoadPageDataAsync()
    {
        var passagesTask = api.GetAdminPassagesPagedAsync(Keyword, ExamId, Part, PageNumber, PageSize);
        var statsTask = api.GetPassageStatsAsync();
        var examsTask = api.GetAdminExamsPagedAsync(null, "All", "All", 1, 100);

        await Task.WhenAll(passagesTask, statsTask, examsTask);

        var paged = await passagesTask;
        if (paged != null)
        {
            Passages = paged.Items;
            TotalCount = paged.TotalCount;
            TotalPages = paged.TotalPages;
            PageNumber = paged.Page;
            PageSize = paged.PageSize;
        }

        var stats = await statsTask;
        if (stats != null)
        {
            TotalPassages = stats.TotalPassages;
            Part6Passages = stats.Part6Passages;
            Part7Passages = stats.Part7Passages;
            ActiveExamPassages = stats.ActiveExamPassages;
        }

        var exams = await examsTask;
        if (exams != null)
        {
            AvailableExams = exams.Items;
        }
    }
}

public class UpsertPassageFormModel
{
    public int ExamId { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public string? Explanation { get; set; }
    public int DisplayOrder { get; set; } = 1;
}
