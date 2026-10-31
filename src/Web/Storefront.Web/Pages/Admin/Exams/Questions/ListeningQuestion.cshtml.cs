using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams.Questions;

public class ListeningQuestionModel(ToeicApiClient api, IWebHostEnvironment env) : PageModel
{
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public bool HasExamFullAudio { get; set; }
    public string? ExamAudioUrl { get; set; }
    public bool IsPart1 => Form.Part == 1;
    public bool IsPart2 => Form.Part == 2;
    public int PartQuestionCount { get; set; }
    public int PartQuestionLimit => Form.Part == 1 ? ExamStructureViewModel.TargetP1 : ExamStructureViewModel.TargetP2;
    public bool CanAddAnother => !IsEditMode && PartQuestionCount < PartQuestionLimit;
    public bool IsPartFull => PartQuestionCount >= PartQuestionLimit;
    public bool IsEditMode { get; set; }

    [BindProperty]
    public ListeningQuestionFormModel Form { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int examId, int part, int? editId = null)
    {
        if (part is not (1 or 2))
        {
            return RedirectToPage("/Admin/Exams/Questions/Listening", new { examId, part = part < 3 ? 3 : part });
        }

        ExamId = examId;
        Form.Part = part;

        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }

        ExamTitle = exam.Title;
        HasExamFullAudio = !string.IsNullOrWhiteSpace(exam.AudioUrl);
        ExamAudioUrl = exam.AudioUrl;
        if (HasExamFullAudio)
        {
            Form.UseExamFullAudio = true;
        }

        PartQuestionCount = exam.Questions.Count(q => q.Part == part);

        if (editId.HasValue)
        {
            IsEditMode = true;
            Form.EditId = editId.Value;

            var question = exam.Questions.FirstOrDefault(q => q.Id == editId.Value && q.Part == part);
            if (question == null)
            {
                TempData["ErrorMessage"] = "Question not found.";
                return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
            }

            Form.QuestionNumber = question.QuestionNumber;
            Form.QuestionText = question.QuestionText;
            Form.Explanation = question.Explanation ?? string.Empty;
            Form.ImageUrl = question.ImageUrl;
            Form.AudioUrl = question.AudioUrl;
            Form.AudioStartSecond = question.AudioStartSecond;
            Form.AudioEndSecond = question.AudioEndSecond;
            Form.UseExamFullAudio = HasExamFullAudio || question.AudioStartSecond.HasValue;
            Form.CorrectOption = question.CorrectOption;
            Form.Difficulty = question.Difficulty ?? "Medium";

            foreach (var opt in question.Options.OrderBy(o => o.OptionLetter))
            {
                switch (opt.OptionLetter)
                {
                    case "A": Form.OptionA = opt.OptionText; break;
                    case "B": Form.OptionB = opt.OptionText; break;
                    case "C": Form.OptionC = opt.OptionText; break;
                    case "D": Form.OptionD = opt.OptionText; break;
                }
            }

            return Page();
        }

        Form.QuestionText = part == 1
            ? "Which description best matches the photograph?"
            : "Select the best response for the question.";

        var maxQNum = exam.Questions.Any() ? exam.Questions.Max(q => q.QuestionNumber) : 0;
        Form.QuestionNumber = maxQNum + 1;

        if (IsPartFull)
        {
            TempData["ErrorMessage"] = $"Part {part} already has the maximum of {PartQuestionLimit} questions.";
            return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int examId)
    {
        return await SaveQuestionAsync(examId, addAnother: false);
    }

    public async Task<IActionResult> OnPostAddAnotherAsync(int examId)
    {
        return await SaveQuestionAsync(examId, addAnother: true);
    }

    private async Task<IActionResult> SaveQuestionAsync(int examId, bool addAnother)
    {
        ExamId = examId;
        IsEditMode = Form.EditId.HasValue && Form.EditId.Value > 0;

        if (Form.Part is not (1 or 2))
        {
            return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
        }

        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }

        ExamTitle = exam.Title;
        HasExamFullAudio = !string.IsNullOrWhiteSpace(exam.AudioUrl);
        ExamAudioUrl = exam.AudioUrl;
        PartQuestionCount = exam.Questions.Count(q => q.Part == Form.Part);

        if (!IsEditMode && IsPartFull)
        {
            ErrorMessage = $"Part {Form.Part} already has the maximum of {PartQuestionLimit} questions.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Form.Explanation))
        {
            ValidationErrors.Add("Explanation is required.");
        }

        if (HasExamFullAudio)
        {
            Form.UseExamFullAudio = true;
            Form.AudioUrl = exam.AudioUrl;
        }
        else if (Form.UseExamFullAudio)
        {
            ErrorMessage = "This exam does not have a full audio file. Upload a separate audio file for this question.";
            return Page();
        }

        if (Form.ImageFile != null && Form.ImageFile.Length > 0)
        {
            Form.ImageUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, Form.ImageFile, "images");
        }

        if (!Form.UseExamFullAudio && Form.AudioFile != null && Form.AudioFile.Length > 0)
        {
            Form.AudioUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, Form.AudioFile, "audio");
        }

        if (Form.Part == 1 && string.IsNullOrWhiteSpace(Form.ImageUrl))
        {
            ValidationErrors.Add("Photograph image is required.");
        }

        if (!Form.UseExamFullAudio && string.IsNullOrWhiteSpace(Form.AudioUrl))
        {
            ValidationErrors.Add("Audio file is required.");
        }

        if (ValidationErrors.Count > 0)
        {
            return Page();
        }

        var req = new AdminSaveListeningQuestionRequest(
            Form.EditId,
            examId,
            Form.Part,
            Form.QuestionNumber,
            Form.QuestionText,
            Form.CorrectOption,
            Form.Explanation,
            Form.UseExamFullAudio ? exam.AudioUrl : Form.AudioUrl,
            Form.AudioStartSecond,
            Form.AudioEndSecond,
            Form.ImageUrl,
            Form.Difficulty,
            Form.OptionA,
            Form.OptionB,
            Form.OptionC,
            Form.Part == 2 ? null : Form.OptionD
        );

        var (savedId, err) = await api.SaveListeningQuestionAsync(examId, req);
        if (savedId.HasValue)
        {
            TempData["SuccessMessage"] = IsEditMode
                ? $"Successfully updated Part {Form.Part} question number {Form.QuestionNumber}."
                : $"Successfully created Part {Form.Part} question number {Form.QuestionNumber}.";

            if (addAnother && !IsEditMode && PartQuestionCount + 1 < PartQuestionLimit)
            {
                return RedirectToPage(new { examId, part = Form.Part });
            }

            return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
        }

        ErrorMessage = err ?? "Failed to save listening question.";
        return Page();
    }
}

public class ListeningQuestionFormModel
{
    public int? EditId { get; set; }
    public int Part { get; set; }
    public int QuestionNumber { get; set; }
    public string? QuestionText { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = "A";
    public string Difficulty { get; set; } = "Medium";
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }

    public string? AudioUrl { get; set; }
    public IFormFile? AudioFile { get; set; }
    public bool UseExamFullAudio { get; set; }
    public int? AudioStartSecond { get; set; }
    public int? AudioEndSecond { get; set; }
}
