using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Progress.Application.Services;

namespace Progress.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProgressController(ProgressService progressService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var result = await progressService.GetSummaryAsync(userId, ct);
        return Ok(result.Value);
    }
}
