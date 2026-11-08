using System.Security.Claims;
using Learning.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Learning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaderboardController(LeaderboardService leaderboardService) : ControllerBase
{
    /// <summary>
    /// UC-4: View Leaderboard (Sorted by Total Score or Study Streak, supports CSV export via Accept: text/csv)
    /// </summary>
    [HttpGet]
    [Produces("application/json", "text/csv")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] string? sortBy = "Score", CancellationToken ct = default)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub") 
            ?? User.FindFirstValue("UserId") 
            ?? "guest";

        var currentUserName = User.FindFirstValue("name") 
            ?? User.FindFirstValue(ClaimTypes.Name) 
            ?? "Default Learner";

        var currentUserEmail = User.FindFirstValue(ClaimTypes.Email) 
            ?? User.FindFirstValue("email") 
            ?? "learner@acadprep.com";

        var result = await leaderboardService.GetLeaderboardAsync(currentUserId, sortBy, currentUserName, currentUserEmail, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}
