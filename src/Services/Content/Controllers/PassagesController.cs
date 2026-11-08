using Content.DTOs;
using Content.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Content.Controllers;

[Authorize(Roles = "Admin,Moderator")]
[ApiController]
[Route("api/cms/[controller]")]
public class PassagesController(IContentService contentService) : ControllerBase
{
    /// <summary>
    /// UC-18.1 View Reading Passages (with search, filter by Exam and Part 6-7, pagination)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPassages(
        [FromQuery] string? keyword,
        [FromQuery] int? examId,
        [FromQuery] int? part,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken ct = default)
    {
        var result = await contentService.GetPagedPassagesAsync(keyword, examId, part, page, pageSize, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get Passage Aggregate Statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetPassageStats(CancellationToken ct)
    {
        var result = await contentService.GetPassageStatsAsync(ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-18.3 View Passage Detail &amp; Attached Part 6-7 Questions
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPassageDetail(int id, CancellationToken ct)
    {
        var result = await contentService.GetPassageDetailAsync(id, ct);
        if (!result.IsSuccess)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-18.2 Create Reading Passage (Part 6-7)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreatePassage([FromBody] UpsertPassageRequestDto dto, CancellationToken ct)
    {
        var result = await contentService.CreatePassageAsync(dto, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id = result.Value, message = "Reading passage created successfully." });
    }

    /// <summary>
    /// UC-18.4 Update Reading Passage
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePassage(int id, [FromBody] UpsertPassageRequestDto dto, CancellationToken ct)
    {
        var result = await contentService.UpdatePassageAsync(id, dto, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id, message = "Reading passage updated successfully." });
    }

    /// <summary>
    /// UC-18.5 Delete Reading Passage (Enforces non-orphan Part 6-7 questions constraint)
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePassage(
        int id,
        [FromQuery] bool deleteWithQuestions = false,
        CancellationToken ct = default)
    {
        var result = await contentService.DeletePassageAsync(id, deleteWithQuestions, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}
