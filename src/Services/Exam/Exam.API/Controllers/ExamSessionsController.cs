using System.Security.Claims;
using Exam.Application.DTOs;
using Exam.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exam.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api")]
public class ExamSessionsController(ExamSessionService sessions, IConfiguration configuration) : ControllerBase
{
    // A guest key is a random bearer secret, separate from Identity user IDs.
    // Never use a shared dummy user: each browser/API client owns its own sessions.
    private string? UserId
    {
        get
        {
            if (User.Identity?.IsAuthenticated == true)
                return User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!configuration.GetValue<bool>("ExamSessions:AllowGuests")) return null;
            var key = Request.Headers["X-Exam-Guest-Key"].ToString();
            return Guid.TryParseExact(key, "N", out var guestId)
                ? $"guest:{guestId:N}" : null;
        }
    }

    [HttpPost("exams/{examId:int}/full-test")]
    public async Task<IActionResult> StartFullTest(int examId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await sessions.StartFullTestAsync(examId, UserId, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetFullTest), new { id = result.Value }, new { sessionId = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("exams/{examId:int}/practice")]
    public async Task<IActionResult> StartPractice(int examId, StartPracticeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await sessions.StartPracticeAsync(examId, UserId, request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPractice), new { id = result.Value }, new { sessionId = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpGet("full-tests/{id:int}")]
    public async Task<IActionResult> GetFullTest(int id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var session = await sessions.GetFullTestAsync(id, UserId, ct);
        return session is null ? NotFound(new { error = "Test session not found." }) : Ok(session);
    }

    [HttpGet("practice-sessions/{id:int}")]
    public async Task<IActionResult> GetPractice(int id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var session = await sessions.GetPracticeAsync(id, UserId, ct);
        return session is null ? NotFound(new { error = "Practice session not found." }) : Ok(session);
    }
}
