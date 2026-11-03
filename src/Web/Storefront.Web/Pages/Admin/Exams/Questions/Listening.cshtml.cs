using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Admin.Exams.Questions;

public class ListeningModel(ToeicApiClient api, IWebHostEnvironment env) : PageModel
{
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public bool HasExamFullAudio { get; set; }
    public string? ExamAudioUrl { get; set; }
    public bool IsEditMode { get; set; }

    [BindProperty]
    public ListeningFormModel Form { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int examId, int part, int? groupId = null)
    {
        if (part is 1 or 2)
        {
            return RedirectToPage("/Admin/Exams/Questions/ListeningQuestion", new { examId, part });
        }

        ExamId = examId;
        Form.Part = part is < 3 or > 4 ? 3 : part;

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

        if (groupId.HasValue)
        {
            IsEditMode = true;
            Form.GroupId = groupId.Value;

            var group = exam.QuestionGroups.FirstOrDefault(g => g.Id == groupId.Value);
            if (group == null)
            {
                TempData["ErrorMessage"] = "Listening group not found.";
                return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
            }

            var questions = exam.Questions.Where(q => q.QuestionGroupId == groupId.Value).OrderBy(q => q.QuestionNumber).ToList();
            if (questions.Count == 0)
            {
                questions = group.Questions.OrderBy(q => q.QuestionNumber).ToList();
            }

            Form.Name = group.Name;
            Form.Explanation = group.Explanation ?? string.Empty;
            Form.ImageUrl = group.ImageUrl;
            Form.AudioUrl = group.AudioUrl;
            Form.AudioStartSecond = group.AudioStartSecond;
            Form.AudioEndSecond = group.AudioEndSecond;
            Form.UseExamFullAudio = HasExamFullAudio || group.AudioStartSecond.HasValue;

            foreach (var q in questions)
            {
                var item = new ListeningQuestionFormItem
                {
                    Id = q.Id,
                    QuestionNumber = q.QuestionNumber,
                    QuestionText = q.QuestionText,
                    ImageUrl = q.ImageUrl,
                    CorrectOption = q.CorrectOption,
                    Difficulty = q.Difficulty ?? "Medium"
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

            while (Form.Questions.Count < 3)
            {
                var nextNum = (Form.Questions.LastOrDefault()?.QuestionNumber ?? 31) + 1;
                Form.Questions.Add(new ListeningQuestionFormItem { QuestionNumber = nextNum });
            }

            return Page();
        }

        Form.Name = Form.Part switch
        {
            3 => "Part 3 - Conversations Set",
            4 => "Part 4 - Talks Set",
            _ => "New listening group"
        };

        var maxQNum = exam.Questions.Any() ? exam.Questions.Max(q => q.QuestionNumber) : (Form.Part == 3 ? 31 : 70);
        var startNum = maxQNum + 1;

        for (int i = 0; i < 3; i++)
        {
            Form.Questions.Add(new ListeningQuestionFormItem
            {
                QuestionNumber = startNum + i,
                QuestionText = "Select the best answer based on the listening content."
            });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int examId)
    {
        if (Form.Part is 1 or 2)
        {
            return RedirectToPage("/Admin/Exams/Questions/ListeningQuestion", new { examId, part = Form.Part });
        }

        ExamId = examId;
        IsEditMode = Form.GroupId.HasValue && Form.GroupId.Value > 0;

        var exam = await api.GetAdminExamAsync(examId);
        if (exam == null || exam.IsDeleted)
        {
            return RedirectToPage("/Admin/Exams/Index");
        }
        ExamTitle = exam.Title;
        HasExamFullAudio = !string.IsNullOrWhiteSpace(exam.AudioUrl);
        ExamAudioUrl = exam.AudioUrl;

        if (Form.Questions.Count != 3)
        {
            ValidationErrors.Add("Part 3 and Part 4 require 3 questions.");
            return Page();
        }

        if (HasExamFullAudio)
        {
            Form.UseExamFullAudio = true;
        }

        string? groupImageUrl = Form.ImageUrl;
        if (Form.ImageFile != null && Form.ImageFile.Length > 0)
        {
            groupImageUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, Form.ImageFile, "images");
        }

        string? groupAudioUrl = Form.UseExamFullAudio ? exam.AudioUrl : Form.AudioUrl;
        if (!Form.UseExamFullAudio && Form.AudioFile != null && Form.AudioFile.Length > 0)
        {
            groupAudioUrl = await QuestionMediaUploadHelper.SaveFileAsync(env, Form.AudioFile, "audio");
        }

        if (string.IsNullOrWhiteSpace(groupAudioUrl))
        {
            groupAudioUrl = "/audio/sample-listening.wav";
        }

        var req = new AdminSaveListeningGroupRequest(
            Form.GroupId,
            examId,
            Form.Part,
            Form.Name,
            groupAudioUrl,
            Form.AudioStartSecond,
            Form.AudioEndSecond,
            groupImageUrl,
            Form.Explanation,
            Form.Questions.Select(q => new AdminSaveListeningGroupQuestionItem(
                q.Id > 0 ? q.Id : null,
                q.QuestionNumber,
                q.QuestionText,
                q.CorrectOption,
                Form.Explanation,
                q.Difficulty,
                q.OptionA,
                q.OptionB,
                q.OptionC,
                q.OptionD
            )).ToList()
        );

        var (savedGroupId, err) = await api.SaveListeningGroupAsync(examId, req);
        if (savedGroupId.HasValue)
        {
            TempData["SuccessMessage"] = IsEditMode
                ? $"Successfully updated listening group '{Form.Name}'."
                : $"Successfully created listening group '{Form.Name}'.";
            return RedirectToPage("/Admin/Exams/Edit", new { id = examId });
        }

        ErrorMessage = err ?? "Failed to save listening group.";
        return Page();
    }
}

public class ListeningFormModel
{
    public int? GroupId { get; set; }
    public int Part { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public bool UseExamFullAudio { get; set; }
    public string? AudioUrl { get; set; }
    public int? AudioStartSecond { get; set; }
    public int? AudioEndSecond { get; set; }
    public string? ImageUrl { get; set; }

    public IFormFile? AudioFile { get; set; }
    public IFormFile? ImageFile { get; set; }

    public List<ListeningQuestionFormItem> Questions { get; set; } = new();
}

public class ListeningQuestionFormItem
{
    public int Id { get; set; }
    public int QuestionNumber { get; set; }
    public string? QuestionText { get; set; }
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
