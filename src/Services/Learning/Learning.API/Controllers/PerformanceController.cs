using System.Security.Claims;
using Learning.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PerformanceController(PerformanceService performanceService) : ControllerBase
{
    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) 
        ?? User.FindFirstValue("sub") 
        ?? User.FindFirstValue("UserId") 
        ?? "user-demo-1";

    /// <summary>
    /// UC-10.2: View Performance Analysis (Listening / Reading strengths & Part 1-7 mastery)
    /// </summary>
    [HttpGet("analysis")]
    public async Task<IActionResult> GetPerformanceAnalysis([FromQuery] string? userId = null, CancellationToken ct = default)
    {
        var targetUserId = !string.IsNullOrWhiteSpace(userId) ? userId : GetCurrentUserId();
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
        var targetUserId = !string.IsNullOrWhiteSpace(userId) ? userId : GetCurrentUserId();
        var result = await performanceService.GetScoreProgressAsync(targetUserId, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}
