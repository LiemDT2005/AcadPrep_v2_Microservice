using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Exams;

public class TakeModel(ToeicApiClient api) : PageModel
{
    public ExamDetail? Exam { get; set; }
    public SubmitExamResult? Result { get; set; }
    public string? Error { get; set; }

    [BindProperty]
    public List<AnswerInput> Answers { get; set; } = [];

    public class AnswerInput
    {
        public Guid QuestionId { get; set; }
        public string Answer { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("jwt")))
            return RedirectToPage("/Account/Login");
        Exam = await api.GetExamAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("jwt")))
            return RedirectToPage("/Account/Login");

        Exam = await api.GetExamAsync(id);
        var payload = new { answers = Answers.Select(a => new { questionId = a.QuestionId, answer = a.Answer }) };
        var (data, error) = await api.SubmitExamAsync(id, payload);
        if (data is null)
        {
            Error = error ?? "Nộp bài thất bại.";
            return Page();
        }
        Result = data;
        return Page();
    }
}
