using System.Security.Claims;
using Exam.DTOs;
using Exam.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exam.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api")]
public class ExamSessionsController(IExamLifecycleService lifecycle, IConfiguration configuration) : ControllerBase
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
        var result = await lifecycle.StartAsync(examId, UserId, null, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetFullTest), new { id = result.Value }, new { sessionId = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("exams/{examId:int}/practice")]
    public async Task<IActionResult> StartPractice(int examId, StartPracticeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await lifecycle.StartAsync(examId, UserId, request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPractice), new { id = result.Value }, new { sessionId = result.Value })
            : BadRequest(new { error = result.Error });
    }

    [HttpGet("full-tests/{id:int}")]
    public async Task<IActionResult> GetFullTest(int id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var session = await lifecycle.GetAsync(id, "full-test", UserId, ct);
        return session is null ? NotFound(new { error = "Test session not found." }) : Ok(session);
    }

    [HttpGet("practice-sessions/{id:int}")]
    public async Task<IActionResult> GetPractice(int id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var session = await lifecycle.GetAsync(id, "practice", UserId, ct);
        return session is null ? NotFound(new { error = "Practice session not found." }) : Ok(session);
    }

    [HttpPut("full-tests/{id:int}/checkpoint")]
    public Task<IActionResult> SaveFull(int id, SaveSessionRequest request, CancellationToken ct) => Save(id, "full-test", request, ct);
    [HttpPut("practice-sessions/{id:int}/checkpoint")]
    public Task<IActionResult> SavePractice(int id, SaveSessionRequest request, CancellationToken ct) => Save(id, "practice", request, ct);
    private async Task<IActionResult> Save(int id, string mode, SaveSessionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await lifecycle.SaveAsync(id, mode, UserId, request, ct);
        return result.IsSuccess ? Ok(new { saved = true }) : BadRequest(new { error = result.Error });
    }
    [HttpPost("full-tests/{id:int}/submit")]
    public Task<IActionResult> SubmitFull(int id, SaveSessionRequest request, CancellationToken ct) => Submit(id, "full-test", request, ct);
    [HttpPost("practice-sessions/{id:int}/submit")]
    public Task<IActionResult> SubmitPractice(int id, SaveSessionRequest request, CancellationToken ct) => Submit(id, "practice", request, ct);
    private async Task<IActionResult> Submit(int id, string mode, SaveSessionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await lifecycle.SubmitAsync(id, mode, UserId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
    [HttpGet("full-tests/{id:int}/review")]
    public Task<IActionResult> ReviewFull(int id, CancellationToken ct) => Review(id, "full-test", ct);
    [HttpGet("practice-sessions/{id:int}/review")]
    public Task<IActionResult> ReviewPractice(int id, CancellationToken ct) => Review(id, "practice", ct);
    private async Task<IActionResult> Review(int id, string mode, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        var result = await lifecycle.ReviewAsync(id, mode, UserId, ct);
        return result is null ? NotFound() : Ok(result);
    }
    [HttpGet("study-history/me")]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        return Ok(await lifecycle.HistoryAsync(UserId, ct));
    }
    [HttpGet("attempts/me")]
    public async Task<IActionResult> Attempts(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        return Ok(await lifecycle.AttemptsAsync(UserId, ct));
    }
    [HttpGet("incorrect-answers/me")]
    public async Task<IActionResult> Incorrect(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(UserId)) return Unauthorized();
        return Ok(await lifecycle.IncorrectAsync(UserId, ct));
    }
}
