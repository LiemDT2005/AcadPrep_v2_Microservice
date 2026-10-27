using System.Security.Claims;
using Learning.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Learning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamificationController(GamificationService gamificationService) : ControllerBase
{
    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) 
        ?? User.FindFirstValue("sub") 
        ?? User.FindFirstValue("UserId") 
        ?? "user-demo-1";

    /// <summary>
    /// UC-13.1: View Achievements (Badges unlocked & progress conditions)
    /// </summary>
    [HttpGet("achievements")]
    public async Task<IActionResult> GetAchievements([FromQuery] string? userId = null, CancellationToken ct = default)
    {
        var targetUserId = !string.IsNullOrWhiteSpace(userId) ? userId : GetCurrentUserId();
        var result = await gamificationService.GetAchievementsAsync(targetUserId, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-13.2: View Study Streak (Continuous learning days with UTC+7 reset rule)
    /// </summary>
    [HttpGet("streak")]
    public async Task<IActionResult> GetStudyStreak([FromQuery] string? userId = null, CancellationToken ct = default)
    {
        var targetUserId = !string.IsNullOrWhiteSpace(userId) ? userId : GetCurrentUserId();
        var result = await gamificationService.GetStudyStreakAsync(targetUserId, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Run maintenance check to reset expired streaks based on UTC+7 midnight rule
    /// </summary>
    [HttpPost("streak/reset-check")]
    public async Task<IActionResult> TriggerStreakResetCheck(CancellationToken ct = default)
    {
        await gamificationService.ResetExpiredStreaksAsync(ct);
        return Ok(new { success = true, message = "Đã quét và làm mới trạng thái chuỗi ngày học thành công." });
    }
}
