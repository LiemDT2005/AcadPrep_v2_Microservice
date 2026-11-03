using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams.Questions;

public class Part6Model(ToeicApiClient api, IWebHostEnvironment env) : PageModel
{
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public bool IsEditMode { get; set; }

    [BindProperty]
    public Part6FormModel Form { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int examId, int? passageId = null)
    {
        ExamId = examId;
        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }

        ExamTitle = exam.Title;

        if (passageId.HasValue)
        {
            IsEditMode = true;
            Form.PassageId = passageId.Value;

            var passage = exam.Passages.FirstOrDefault(p => p.Id == passageId.Value);
            if (passage == null)
            {
                TempData["ErrorMessage"] = "Passage not found.";
                return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
            }

            var questions = exam.Questions
                .Where(q => q.Part == 6 && q.PassageId == passageId.Value)
                .OrderBy(q => q.QuestionNumber)
                .ToList();

            Form.PassageContent = passage.Content;
            Form.ImageUrl = passage.ImageUrl;
            Form.Explanation = passage.Explanation ?? string.Empty;
            Form.PassageInputMode = !string.IsNullOrWhiteSpace(passage.ImageUrl) && string.IsNullOrWhiteSpace(passage.Content) ? "image" : "text";

            foreach (var q in questions)
            {
                var item = new Part6QuestionFormItem
                {
                    Id = q.Id,
                    QuestionNumber = q.QuestionNumber,
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

            while (Form.Questions.Count < 4)
            {
                var nextNum = (Form.Questions.LastOrDefault()?.QuestionNumber ?? 130) + 1;
                Form.Questions.Add(new Part6QuestionFormItem { QuestionNumber = nextNum });
            }

            return Page();
        }

        var maxQNum = exam.Questions.Where(q => q.Part == 6).Select(q => (int?)q.QuestionNumber).Max();
        var startNum = maxQNum.HasValue ? maxQNum.Value + 1 : 131;

        for (int i = 0; i < 4; i++)
        {
            Form.Questions.Add(new Part6QuestionFormItem
            {
                QuestionNumber = startNum + i
            });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int examId)
    {
        ExamId = examId;
        IsEditMode = Form.PassageId.HasValue && Form.PassageId.Value > 0;

        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }
        ExamTitle = exam.Title;

        if (Form.Questions.Count != 4)
        {
            ValidationErrors.Add("Part 6 requires 4 questions.");
            return Page();
        }

        string? imageUrl = Form.ImageUrl;
        if (Form.PassageInputMode == "image" && Form.ImageFile != null && Form.ImageFile.Length > 0)
        {
            imageUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, Form.ImageFile, "images");
        }

        var req = new AdminSavePart6SetRequest(
            Form.PassageId,
            examId,
            Form.PassageInputMode == "text" ? Form.PassageContent : null,
            Form.PassageInputMode == "image" ? imageUrl : null,
            Form.Explanation,
            1,
            Form.Questions.Select(q => new AdminSavePart6QuestionItem(
                q.Id > 0 ? q.Id : null,
                q.QuestionNumber,
                $"Blank ({q.QuestionNumber})",
                q.CorrectOption,
                Form.Explanation,
                "Medium",
                q.OptionA,
                q.OptionB,
                q.OptionC,
                q.OptionD
            )).ToList()
        );

        var (savedPassageId, err) = await api.SavePart6SetAsync(examId, req);
        if (savedPassageId.HasValue)
        {
            TempData["SuccessMessage"] = IsEditMode
                ? $"Successfully updated Part 6 passage (Q{Form.Questions.First().QuestionNumber} - Q{Form.Questions.Last().QuestionNumber})."
                : $"Successfully created Part 6 passage (Q{Form.Questions.First().QuestionNumber} - Q{Form.Questions.Last().QuestionNumber}).";
            return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
        }

        ErrorMessage = err ?? "Failed to save Part 6 passage.";
        return Page();
    }
}

public class Part6FormModel
{
    public int? PassageId { get; set; }
    public string PassageInputMode { get; set; } = "text";
    public string? PassageContent { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }
    public List<Part6QuestionFormItem> Questions { get; set; } = new();
}

public class Part6QuestionFormItem
{
    public int Id { get; set; }
    public int QuestionNumber { get; set; }
    public string CorrectOption { get; set; } = "A";
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
}
