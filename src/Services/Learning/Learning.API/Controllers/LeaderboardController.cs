using System.Security.Claims;
using Learning.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Learning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController(LeaderboardService leaderboardService) : ControllerBase
{
    /// <summary>
    /// UC-4: View Leaderboard (Sorted by Total Score or Study Streak)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] string? sortBy = "Score", CancellationToken ct = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub") 
            ?? User.FindFirstValue("UserId") 
            ?? "guest";

        var result = await leaderboardService.GetLeaderboardAsync(currentUserId, sortBy, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}
