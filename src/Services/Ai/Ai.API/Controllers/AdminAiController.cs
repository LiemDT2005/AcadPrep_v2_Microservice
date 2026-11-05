using Ai.API.DTOs;
using Ai.API.Services;
using Ai.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ai.API.Controllers;

// UC-22.2. Admin only.
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/ai")]
public class AdminAiController(IAdminUsageService admin, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<IActionResult> Usage([FromQuery] AdminUsageQuery query, CancellationToken ct) =>
        Ok(await admin.ListAsync(query, ct));

    [HttpGet("limits")]
    public async Task<IActionResult> Limits(CancellationToken ct) => Ok(await admin.LimitsAsync(ct));

    [HttpPut("limits/{planCode}")]
    public async Task<IActionResult> UpdateLimit(string planCode, UpdateTokenLimitRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } adminId) return Unauthorized();
        if (!AdminUsageService.Plans.Contains(planCode)) return Problem(statusCode: 404, title: "Plan not found.");
        var updated = await admin.UpdateLimitAsync(adminId, planCode, request.TokenLimit!.Value, ct);
        return updated is null ? Problem(statusCode: 404, title: "Plan not found.") : Ok(updated);
    }

    [HttpPost("usage/{userId:guid}/reset")]
    public async Task<IActionResult> Reset(Guid userId, CancellationToken ct)
    {
        if (currentUser.UserId is not { } adminId) return Unauthorized();
        if (userId == Guid.Empty) return Problem(statusCode: 404, title: "User not found.");
        await admin.ResetAsync(adminId, userId, ct);
        return NoContent();
    }
}
