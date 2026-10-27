using System.Security.Cryptography;
using System.Text;
using Content.Data;
using Content.Entities.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Content.Controllers;

[ApiController]
[Route("internal/exam-papers")]
public class ExamPaperController(ContentDbContext db, IConfiguration configuration) : ControllerBase
{
    // Answer keys are available only to the Exam service, never in the public learner API.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var expected = configuration["InternalApiKey"];
        var supplied = Request.Headers["X-Internal-Key"].ToString();
        if (string.IsNullOrEmpty(expected)) return StatusCode(503);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)))) return Unauthorized();
        var exam = await db.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted && e.Status == ExamStatus.Published, ct);
        if (exam is null) return NotFound();
        var questions = await db.Questions.AsNoTracking().Where(q => q.ExamId == id)
            .Include(q => q.QuestionOptions).Include(q => q.Passage)
            .Include(q => q.QuestionGroup).ThenInclude(g => g!.Passages)
            .AsSplitQuery().OrderBy(q => q.Part).ThenBy(q => q.QuestionNumber).ToListAsync(ct);
        return Ok(new
        {
            exam.Id, exam.Title, exam.Duration,
            Questions = questions.Select(q => new
            {
                QuestionId = q.Id, q.QuestionNumber, q.Part, q.QuestionText, q.TopicTag,
                ImageUrl = q.ImageUrl ?? q.QuestionGroup?.ImageUrl,
                AudioUrl = q.Part <= 4 ? q.AudioUrl ?? q.QuestionGroup?.AudioUrl ?? exam.AudioUrl : null,
                AudioStartSecond = q.AudioStartSecond ?? q.QuestionGroup?.AudioStartSecond,
                AudioEndSecond = q.AudioEndSecond ?? q.QuestionGroup?.AudioEndSecond,
                q.QuestionGroupId, q.PassageId, CorrectOption = q.CorrectOption.ToString(),
                Explanation = q.Explanation ?? q.QuestionGroup?.Explanation ?? q.Passage?.Explanation,
                PassageContent = q.Passage?.Content ?? string.Join("\n\n", q.QuestionGroup?.Passages.OrderBy(p => p.DisplayOrder).Select(p => p.Content) ?? []),
                PassageImageUrl = q.Passage?.ImageUrl,
                Passages = (q.QuestionGroup?.Passages.Count > 0 ? q.QuestionGroup.Passages
                    : q.Passage is not null ? new[] { q.Passage }.AsEnumerable() : [])
                    .OrderBy(p => p.DisplayOrder).Select(p => new { p.Id, p.DisplayOrder, p.Content, p.ImageUrl }),
                Options = q.QuestionOptions.OrderBy(o => o.OptionLetter).Select(o => new { Letter = o.OptionLetter.ToString(), Text = o.OptionText })
            })
        });
    }
}
