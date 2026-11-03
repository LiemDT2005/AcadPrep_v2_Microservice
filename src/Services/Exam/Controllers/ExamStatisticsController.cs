using System.Security.Cryptography;
using System.Text;
using Exam.Data;
using Exam.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Exam.Controllers;

[ApiController]
[Route("internal/exam-statistics")]
public class ExamStatisticsController(ExamDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(expected)) return StatusCode(503);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(Request.Headers["X-Internal-Key"].ToString())))) return Unauthorized();
        var states = await db.SessionStates.AsNoTracking().Where(s => s.Id.StartsWith("full-test:")).ToListAsync(ct);
        var ids = states.Select(s => int.Parse(s.Id.Split(':')[1])).ToHashSet();
        var legacy = await db.ExamAttempts.AsNoTracking().Include(a => a.Exam).Where(a => !ids.Contains(a.Id)).ToListAsync(ct);
        var rows = states.Select(s => new { s.ExamId, Title = s.ExamTitle, s.IsSubmitted, Score = s.ListeningScore + s.ReadingScore })
            .Concat(legacy.Select(a => new { a.ExamId, Title = a.Exam.Title, a.IsSubmitted, Score = a.TotalScore }));
        return Ok(rows.GroupBy(x => x.ExamId).Select(g =>
        {
            var scores = g.Where(x => x.IsSubmitted).Select(x => x.Score).ToList();
            return new
            {
                ExamId = ExamLifecycleService.StableId($"exam:{g.Key}"), ExamName = g.First().Title,
                TotalAttempts = g.Count(), AverageScore = scores.Count > 0 ? Math.Round(scores.Average(), 1) : 0,
                HighestScore = scores.Count > 0 ? scores.Max() : 0, LowestScore = scores.Count > 0 ? scores.Min() : 0,
                CompletionRate = Math.Round(scores.Count * 100.0 / g.Count(), 1)
            };
        }).OrderByDescending(x => x.TotalAttempts).ToList());
    }
}
