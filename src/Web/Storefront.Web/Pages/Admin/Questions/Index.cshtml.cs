using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Pages.Admin.Exams.Questions;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Questions;

public class IndexModel(ToeicApiClient api, IWebHostEnvironment env) : PageModel
{
    public PagedResult<AdminQuestionListItem> Questions { get; set; } = new([], 0, 1, 15, 0);
    public List<AdminExamListItem> Exams { get; set; } = [];

    public int TotalQuestionsCount { get; set; }
    public int ListeningQuestionsCount { get; set; }
    public int ReadingQuestionsCount { get; set; }
    public int ActiveExamQuestionsCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FilterPart { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterDifficulty { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FilterExamId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageIndex { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 15;

    [BindProperty(SupportsGet = true)]
    public bool Create { get; set; }

    [BindProperty]
    public UpsertQuestionFormModel Form { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        await LoadExamsAsync();

        string? imageUrl = Form.ImageUrl;
        if (Form.ImageFile != null && Form.ImageFile.Length > 0)
        {
            imageUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, Form.ImageFile, "images");
        }

        string? audioUrl = Form.AudioUrl;
        if (Form.AudioFile != null && Form.AudioFile.Length > 0)
        {
            audioUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, Form.AudioFile, "audio");
        }

        var req = new AdminUpsertQuestionRequest(
            Form.ExamId,
            Form.Part,
            Form.QuestionNumber,
            Form.QuestionText,
            Form.CorrectOption,
            Form.Explanation,
            audioUrl,
            Form.AudioStartSecond,
            Form.AudioEndSecond,
            imageUrl,
            Form.QuestionType,
            Form.TopicTag,
            Form.Difficulty,
            null,
            null,
            Form.OptionA,
            Form.OptionB,
            Form.OptionC,
            Form.Part == 2 ? null : Form.OptionD
        );

        if (Form.QuestionId.HasValue && Form.QuestionId.Value > 0)
        {
            var (ok, err) = await api.UpdateAdminQuestionAsync(Form.QuestionId.Value, req);
            if (ok)
            {
                SuccessMessage = $"Question #{Form.QuestionNumber} (Part {Form.Part}) updated successfully.";
                return RedirectToPage("./Index", new { SearchTerm, FilterPart, FilterDifficulty, FilterExamId, PageIndex });
            }
            ErrorMessage = err ?? "Failed to update question.";
        }
        else
        {
            var (newId, err) = await api.CreateAdminQuestionAsync(req);
            if (newId.HasValue)
            {
                SuccessMessage = $"Question #{Form.QuestionNumber} (Part {Form.Part}) created successfully.";
                return RedirectToPage("./Index", new { SearchTerm, FilterPart, FilterDifficulty, FilterExamId, PageIndex });
            }
            ErrorMessage = err ?? "Failed to create question.";
        }

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int deleteId)
    {
        if (deleteId <= 0)
        {
            ErrorMessage = "Invalid question ID.";
            return RedirectToPage("./Index", new { SearchTerm, FilterPart, FilterDifficulty, FilterExamId, PageIndex });
        }

        var (res, err) = await api.DeleteAdminQuestionAsync(deleteId);
        if (res != null)
        {
            SuccessMessage = res.Message;
        }
        else
        {
            ErrorMessage = err ?? "Failed to delete question.";
        }

        return RedirectToPage("./Index", new { SearchTerm, FilterPart, FilterDifficulty, FilterExamId, PageIndex });
    }

    private async Task LoadExamsAsync()
    {
        var examsPaged = await api.GetAdminExamsPagedAsync(pageNumber: 1, pageSize: 100);
        Exams = examsPaged?.Items.Where(e => !e.IsDeleted).ToList() ?? [];
    }

    private async Task LoadDataAsync()
    {
        if (PageIndex < 1) PageIndex = 1;
        if (PageSize < 5) PageSize = 15;

        var examsTask = api.GetAdminExamsPagedAsync(pageNumber: 1, pageSize: 100);
        var statsTask = api.GetQuestionStatsAsync();
        var pagedTask = api.GetAdminQuestionsPagedAsync(
            SearchTerm,
            FilterPart,
            FilterDifficulty,
            FilterExamId,
            PageIndex,
            PageSize);

        await Task.WhenAll(examsTask, statsTask, pagedTask);

        var examsPaged = await examsTask;
        Exams = examsPaged?.Items.Where(e => !e.IsDeleted).ToList() ?? [];

        var stats = await statsTask;
        if (stats != null)
        {
            TotalQuestionsCount = stats.TotalQuestions;
            ListeningQuestionsCount = stats.ListeningQuestions;
            ReadingQuestionsCount = stats.ReadingQuestions;
            ActiveExamQuestionsCount = stats.ActiveExamQuestions;
        }

        var paged = await pagedTask;
        if (paged != null)
        {
            Questions = paged;
        }
    }
}

public class UpsertQuestionFormModel
{
    public int? QuestionId { get; set; }
    public int ExamId { get; set; } = 1;
    public int Part { get; set; } = 5;
    public int QuestionNumber { get; set; } = 105;
    public string? QuestionText { get; set; }
    public string CorrectOption { get; set; } = "A";
    public string? Explanation { get; set; }
    public string? QuestionType { get; set; }
    public string? TopicTag { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public string? AudioUrl { get; set; }
    public int? AudioStartSecond { get; set; }
    public int? AudioEndSecond { get; set; }
    public string? ImageUrl { get; set; }
    public IFormFile? AudioFile { get; set; }
    public IFormFile? ImageFile { get; set; }
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string? OptionD { get; set; }
}
