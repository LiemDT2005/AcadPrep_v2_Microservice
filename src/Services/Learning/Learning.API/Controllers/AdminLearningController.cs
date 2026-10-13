using Learning.Application.DTOs;
using Learning.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Learning.API.Controllers;

[ApiController]
[Route("api/admin/learning")]
public class AdminLearningController(
    LearningAdminService adminService,
    GamificationService gamificationService) : ControllerBase
{
    /// <summary>
    /// UC-20.1: View Learning Progress Reports (Aggregated test volume over date range, Cached with Redis)
    /// </summary>
    [HttpGet("reports")]
    public async Task<IActionResult> GetProgressReport(
        [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct = default)
    {
        var result = await adminService.GetProgressReportAsync(startDate, endDate, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-20.2: View User Statistics (New registrations, active users, retention rate, Cached with Redis)
    /// </summary>
    [HttpGet("user-stats")]
    public async Task<IActionResult> GetUserStatistics(CancellationToken ct = default)
    {
        var result = await adminService.GetUserStatisticsAsync(ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-20.3: View Exam Statistics (Attempts, average/highest/lowest scores per exam, Cached with Redis)
    /// </summary>
    [HttpGet("exam-stats")]
    public async Task<IActionResult> GetExamStatistics(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var result = await adminService.GetExamStatisticsAsync(pageNumber, pageSize, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Admin: Create New Achievement Badge
    /// </summary>
    [HttpPost("achievements")]
    public async Task<IActionResult> CreateAchievement(
        [FromBody] CreateAchievementRequest request, CancellationToken ct = default)
    {
        var result = await gamificationService.CreateAchievementAsync(request, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetProgressReport), new { id = result.Value!.AchievementId }, result.Value);
    }

    /// <summary>
    /// Admin: Update Existing Achievement Badge
    /// </summary>
    [HttpPut("achievements/{id:int}")]
    public async Task<IActionResult> UpdateAchievement(
        int id, [FromBody] UpdateAchievementRequest request, CancellationToken ct = default)
    {
        var result = await gamificationService.UpdateAchievementAsync(id, request, ct);
        if (!result.IsSuccess) return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Admin: Delete Achievement Badge
    /// </summary>
    [HttpDelete("achievements/{id:int}")]
    public async Task<IActionResult> DeleteAchievement(int id, CancellationToken ct = default)
    {
        var result = await gamificationService.DeleteAchievementAsync(id, ct);
        if (!result.IsSuccess) return NotFound(new { error = result.Error });

        return Ok(new { success = true, message = "Đã xóa danh hiệu thành công." });
    }
}
