using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams.Questions;

public class Part5Model(ToeicApiClient api) : PageModel
{
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;

    [BindProperty]
    public Part5FormModel Form { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public int PartQuestionCount { get; set; }
    public int PartQuestionLimit { get; set; } = ExamStructureViewModel.TargetP5;
    public bool CanAddAnother => !IsEditMode && PartQuestionCount < PartQuestionLimit;
    public bool IsPartFull => PartQuestionCount >= PartQuestionLimit;
    public bool IsEditMode { get; set; }

    public async Task<IActionResult> OnGetAsync(int examId, int? editId = null)
    {
        ExamId = examId;
        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }

        ExamTitle = exam.Title;
        PartQuestionCount = exam.Questions.Count(q => q.Part == 5);

        if (editId.HasValue)
        {
            IsEditMode = true;
            Form.EditId = editId.Value;

            var question = exam.Questions.FirstOrDefault(q => q.Id == editId.Value && q.Part == 5);
            if (question == null)
            {
                TempData["ErrorMessage"] = "Question not found.";
                return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
            }

            Form.QuestionNumber = question.QuestionNumber;
            Form.QuestionText = question.QuestionText ?? string.Empty;
            Form.CorrectOption = question.CorrectOption;
            Form.QuestionType = question.QuestionType;
            Form.TopicTag = question.TopicTag;
            Form.Difficulty = question.Difficulty ?? "Medium";
            Form.Explanation = question.Explanation ?? string.Empty;

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

        var maxQNum = exam.Questions.Where(q => q.Part == 5).Select(q => (int?)q.QuestionNumber).Max();
        if (!maxQNum.HasValue && exam.Questions.Any())
        {
            maxQNum = Math.Max(100, exam.Questions.Max(q => q.QuestionNumber));
        }

        Form.QuestionNumber = (maxQNum ?? 100) + 1;

        if (IsPartFull)
        {
            TempData["ErrorMessage"] = $"Part 5 already has the maximum of {PartQuestionLimit} questions.";
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

        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }
        ExamTitle = exam.Title;
        PartQuestionCount = exam.Questions.Count(q => q.Part == 5);

        if (string.IsNullOrWhiteSpace(Form.QuestionText))
        {
            ValidationErrors.Add("Question text is required.");
        }
        if (string.IsNullOrWhiteSpace(Form.Explanation))
        {
            ValidationErrors.Add("Explanation is required.");
        }
        if (ValidationErrors.Count > 0)
        {
            return Page();
        }

        var req = new AdminSavePart5QuestionRequest(
            Form.EditId,
            examId,
            Form.QuestionNumber,
            Form.QuestionText,
            Form.CorrectOption,
            Form.Explanation,
            Form.QuestionType,
            Form.TopicTag,
            Form.Difficulty,
            Form.OptionA,
            Form.OptionB,
            Form.OptionC,
            Form.OptionD
        );

        var (savedId, err) = await api.SavePart5QuestionAsync(examId, req);
        if (savedId.HasValue)
        {
            TempData["SuccessMessage"] = IsEditMode
                ? $"Successfully updated Part 5 question number {Form.QuestionNumber}."
                : $"Successfully created Part 5 question number {Form.QuestionNumber}.";

            if (addAnother && !IsEditMode && PartQuestionCount + 1 < PartQuestionLimit)
            {
                return RedirectToPage(new { examId });
            }

            return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
        }

        ErrorMessage = err ?? "Failed to save Part 5 question.";
        return Page();
    }
}

public class Part5FormModel
{
    public int? EditId { get; set; }
    public int QuestionNumber { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = "A";
    public string? QuestionType { get; set; }
    public string? TopicTag { get; set; }
    public string Difficulty { get; set; } = "Medium";
    public string Explanation { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
}
