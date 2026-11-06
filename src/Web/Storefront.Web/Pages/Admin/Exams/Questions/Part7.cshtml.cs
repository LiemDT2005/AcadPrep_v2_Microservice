using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams.Questions;

public class Part7Model(ToeicApiClient api, IWebHostEnvironment env) : PageModel
{
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public bool IsEditMode { get; set; }
    public int PartQuestionCount { get; set; }
    public int PartQuestionLimit => ExamStructureViewModel.TargetP7;
    public bool IsPartFull => PartQuestionCount + 2 > PartQuestionLimit;

    [BindProperty]
    public Part7FormModel Form { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int examId, int? groupId = null)
    {
        ExamId = examId;
        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }

        ExamTitle = exam.Title;

        if (groupId.HasValue)
        {
            IsEditMode = true;
            Form.GroupId = groupId.Value;

            var group = exam.QuestionGroups.FirstOrDefault(g => g.Id == groupId.Value);
            if (group == null)
            {
                TempData["ErrorMessage"] = "Reading set not found.";
                return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
            }

            Form.Name = group.Name;
            Form.Explanation = group.Explanation ?? string.Empty;

            var passages = group.Passages.Any()
                ? group.Passages.OrderBy(p => p.DisplayOrder).ToList()
                : exam.Passages.Where(p => p.QuestionGroupId == groupId.Value).OrderBy(p => p.DisplayOrder).ToList();

            foreach (var p in passages)
            {
                Form.Passages.Add(new Part7PassageFormItem
                {
                    Id = p.Id,
                    Content = p.Content,
                    ImageUrl = p.ImageUrl,
                    DisplayOrder = p.DisplayOrder
                });
            }

            var questions = exam.Questions.Where(q => q.QuestionGroupId == groupId.Value).OrderBy(q => q.QuestionNumber).ToList();
            if (!questions.Any())
            {
                questions = group.Questions.OrderBy(q => q.QuestionNumber).ToList();
            }

            foreach (var q in questions)
            {
                var item = new Part7QuestionFormItem
                {
                    Id = q.Id,
                    QuestionNumber = q.QuestionNumber,
                    QuestionText = q.QuestionText ?? string.Empty,
                    CorrectOption = q.CorrectOption
                };

                foreach (var opt in q.Options.OrderBy(o => o.OptionLetter))
                {
                    switch (opt.OptionLetter)
                    {
                        case "A": item.OptionA = opt.OptionText; break;
                        case "B": item.OptionB = opt.OptionText; break;
                        case "C": item.OptionC = opt.OptionText; break;
                        case "D": item.OptionD = opt.OptionText; break;
                    }
                }

                Form.Questions.Add(item);
            }

            return Page();
        }

        PartQuestionCount = exam.Questions.Count(q => q.Part == 7);
        if (IsPartFull)
        {
            TempData["ErrorMessage"] = $"Part 7 already has the maximum of {PartQuestionLimit} questions.";
            return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
        }

        var existingP7Sets = exam.QuestionGroups.Count(g => g.Questions.Any(q => q.Part == 7));
        Form.Name = $"Set {existingP7Sets + 1:D2} - Reading Comprehension";

        var maxQNum = exam.Questions.Where(q => q.Part == 7).Select(q => (int?)q.QuestionNumber).Max();
        var startNum = maxQNum.HasValue ? maxQNum.Value + 1 : 147;

        Form.Passages.Add(new Part7PassageFormItem { Content = "Dear Mr. Lee, We are writing to confirm...", DisplayOrder = 1 });
        Form.Questions.Add(new Part7QuestionFormItem { QuestionNumber = startNum, QuestionText = "What is the main purpose of the email?", CorrectOption = "A" });
        Form.Questions.Add(new Part7QuestionFormItem { QuestionNumber = startNum + 1, QuestionText = "What is indicated about Mr. Lee?", CorrectOption = "B" });

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int examId)
    {
        ExamId = examId;
        IsEditMode = Form.GroupId.HasValue && Form.GroupId.Value > 0;

        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }
        ExamTitle = exam.Title;

        PartQuestionCount = exam.Questions.Count(q => q.Part == 7);
        if (!IsEditMode && PartQuestionCount + Form.Questions.Count > PartQuestionLimit)
        {
            ErrorMessage = $"Adding {Form.Questions.Count} question(s) exceeds Part 7 limit ({PartQuestionCount}/{PartQuestionLimit} questions).";
            return Page();
        }

        var validPassages = new List<AdminSavePart7PassageItem>();
        for (int i = 0; i < Form.Passages.Count; i++)
        {
            var p = Form.Passages[i];
            string? imageUrl = p.ImageUrl;
            if (p.ImageFile != null && p.ImageFile.Length > 0)
            {
                imageUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, p.ImageFile, "images");
            }

            if (!string.IsNullOrWhiteSpace(p.Content) || !string.IsNullOrWhiteSpace(imageUrl))
            {
                validPassages.Add(new AdminSavePart7PassageItem(
                    p.Id > 0 ? p.Id : null,
                    p.Content,
                    imageUrl,
                    Form.Explanation,
                    i + 1
                ));
            }
        }

        if (validPassages.Count == 0)
        {
            ValidationErrors.Add("At least one passage must have content or an image.");
            return Page();
        }

        if (Form.Questions.Count < 2)
        {
            ValidationErrors.Add("Part 7 requires at least 2 questions.");
            return Page();
        }

        var req = new AdminSavePart7SetRequest(
            Form.GroupId,
            examId,
            Form.Name,
            Form.Explanation,
            validPassages,
            Form.Questions.Select(q => new AdminSavePart7QuestionItem(
                q.Id > 0 ? q.Id : null,
                q.QuestionNumber,
                q.QuestionText,
                q.CorrectOption,
                Form.Explanation,
                "Medium",
                q.OptionA,
                q.OptionB,
                q.OptionC,
                q.OptionD
            )).ToList()
        );

        var (savedGroupId, err) = await api.SavePart7SetAsync(examId, req);
        if (savedGroupId.HasValue)
        {
            TempData["SuccessMessage"] = IsEditMode
                ? $"Successfully updated Reading Set '{Form.Name}'."
                : $"Successfully created Reading Set '{Form.Name}'.";
            return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
        }

        ErrorMessage = err ?? "Failed to save Reading Set.";
        return Page();
    }
}

public class Part7FormModel
{
    public int? GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public List<Part7PassageFormItem> Passages { get; set; } = new();
    public List<Part7QuestionFormItem> Questions { get; set; } = new();
}

public class Part7PassageFormItem
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
    public int DisplayOrder { get; set; } = 1;
}

public class Part7QuestionFormItem
{
    public int Id { get; set; }
    public int QuestionNumber { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = "A";
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
}
