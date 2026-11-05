using Ai.API.DTOs;
using Ai.API.Services;
using Ai.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ai.API.Controllers;

// UC-22.1 + usage. Learner only (AI chat is Learner-only in this version, Huy 2026-11-05).
[ApiController]
[Authorize(Roles = "Learner")]
[Route("api/ai")]
public class ChatController(IChatService chat, ISessionService sessions, IUsageService usage, ICurrentUser currentUser)
    : ControllerBase
{
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Chat(ChatRequestDto request)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        // Not tied to the browser connection: if the client disconnects after Gemini answered, the result is still
        // stored and a retry with the same RequestId returns it without charging again.
        var result = await chat.SendAsync(userId, request, CancellationToken.None);
        return result.Outcome switch
        {
            ChatOutcome.Ok => Ok(result.Response),
            ChatOutcome.NotFound => Problem(statusCode: 404, title: result.Message),
            ChatOutcome.InProgress => Problem(statusCode: 409, title: result.Message),
            ChatOutcome.QuotaExceeded => QuotaProblem(result),
            ChatOutcome.Unavailable => Problem(statusCode: 503, title: result.Message),
            _ => Problem(statusCode: 502, title: result.Message)
        };
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions([FromQuery] PageQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await sessions.ListAsync(userId, query, ct));
    }

    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, [FromQuery] PageQuery query, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var page = await sessions.MessagesAsync(userId, id, query, ct);
        return page is null ? Problem(statusCode: 404, title: "Session not found.") : Ok(page);
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return await sessions.DeleteAsync(userId, id, ct) ? NoContent() : Problem(statusCode: 404, title: "Session not found.");
    }

    // Also consumed by Billing UC-23.2. Read-only: never creates a window.
    [HttpGet("usage/me")]
    [ProducesResponseType(typeof(UsageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Usage(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        try { return Ok(await usage.GetMyUsageAsync(userId, ct)); }
        catch (AiConfigurationException) { return Problem(statusCode: 503, title: "Token limits are not configured."); }
    }

    // 7-day rolling chart for the "Xem tất cả" panel.
    [HttpGet("usage/me/daily")]
    [ProducesResponseType(typeof(DailyUsageResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Daily(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        try { return Ok(await usage.GetMyDailyUsageAsync(userId, ct)); }
        catch (AiConfigurationException) { return Problem(statusCode: 503, title: "Token limits are not configured."); }
    }

    private ObjectResult QuotaProblem(ChatResult result)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, 429,
            result.Message == "MessageTooLarge" ? "The message is too long for the remaining token budget."
                : "Daily AI token limit reached.");
        problem.Extensions["code"] = result.Message;
        problem.Extensions["usage"] = result.Usage;
        return new ObjectResult(problem) { StatusCode = 429 };
    }
}
