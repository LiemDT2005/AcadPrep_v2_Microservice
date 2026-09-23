using System.Security.Claims;
using Exam.Application.DTOs;
using Exam.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exam.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController(ExamService examService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await examService.GetAllAsync(ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await examService.GetByIdAsync(id, ct);
        if (!result.IsSuccess) return NotFound(new { error = result.Error });
        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitExamRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await examService.SubmitAsync(id, userId, request, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });
        return Ok(result.Value);
    }
}
