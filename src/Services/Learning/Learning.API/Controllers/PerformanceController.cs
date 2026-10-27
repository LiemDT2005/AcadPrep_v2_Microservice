using System.Security.Claims;
using Learning.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learning.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PerformanceController(PerformanceService performanceService) : ControllerBase
{
    private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

    [HttpGet("study-history")]
    public async Task<IActionResult> GetVocabularyHistory(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var result = await performanceService.GetVocabularyHistoryAsync(userId, ct);
        return Ok(result.Value);
    }

    /// <summary>
    /// UC-10.2: View Performance Analysis (Listening / Reading strengths & Part 1-7 mastery)
    /// </summary>
    [HttpGet("analysis")]
    public async Task<IActionResult> GetPerformanceAnalysis([FromQuery] string? userId = null, CancellationToken ct = default)
    {
        var targetUserId = GetCurrentUserId();
        if (string.IsNullOrEmpty(targetUserId)) return Unauthorized();
        if (!string.IsNullOrEmpty(userId) && userId != targetUserId) return Forbid();
        var result = await performanceService.GetPerformanceAnalysisAsync(targetUserId, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-10.3: View Score Progress (Line chart data across exam attempts)
    /// </summary>
    [HttpGet("progress")]
    public async Task<IActionResult> GetScoreProgress([FromQuery] string? userId = null, CancellationToken ct = default)
    {
        var targetUserId = GetCurrentUserId();
        if (string.IsNullOrEmpty(targetUserId)) return Unauthorized();
        if (!string.IsNullOrEmpty(userId) && userId != targetUserId) return Forbid();
        var result = await performanceService.GetScoreProgressAsync(targetUserId, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}
