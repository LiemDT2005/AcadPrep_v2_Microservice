using System.Security.Claims;
using Exam.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exam.Controllers;

[ApiController]
[Route("api/exams/{examId:int}/attempts")]
public class ExamAttemptsController(IExamAttemptService attempts) : ControllerBase
{
    // Aggregate only; no learner identities or answers are exposed.
    [AllowAnonymous]
    [HttpGet("count")]
    public async Task<IActionResult> GetCount(int examId, CancellationToken ct) =>
        Ok(await attempts.GetAttemptCountAsync(examId, ct));

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyHistory(int examId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        return Ok(await attempts.GetHistoryAsync(examId, userId, ct));
    }
}
