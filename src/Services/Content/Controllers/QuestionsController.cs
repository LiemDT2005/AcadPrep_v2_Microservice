using Content.DTOs;
using Content.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Content.Controllers;

[ApiController]
[Route("api/cms/[controller]")]
public class QuestionsController(IContentService contentService) : ControllerBase
{
    /// <summary>
    /// UC-17.1 View Questions / UC-17.6 Search Questions / UC-17.7 Filter Questions (Part 1-7 &amp; Difficulty)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetQuestions(
        [FromQuery] string? keyword,
        [FromQuery] int? part,
        [FromQuery] string? difficulty,
        [FromQuery] int? examId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken ct = default)
    {
        var result = await contentService.GetPagedQuestionsAsync(keyword, part, difficulty, examId, page, pageSize, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get Question Aggregate Statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetQuestionStats(CancellationToken ct)
    {
        var result = await contentService.GetQuestionStatsAsync(ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-17.3 View Question Detail &amp; Embedded Exams
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetQuestionDetail(int id, CancellationToken ct)
    {
        var result = await contentService.GetQuestionDetailAsync(id, ct);
        if (!result.IsSuccess)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-17.2 Create Question + Options + Assign Part 1-7
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateQuestion([FromBody] UpsertQuestionRequestDto dto, CancellationToken ct)
    {
        var result = await contentService.CreateQuestionAsync(dto, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id = result.Value, message = "Question created successfully." });
    }

    /// <summary>
    /// UC-17.4 Update Question / Audio / Options
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateQuestion(int id, [FromBody] UpsertQuestionRequestDto dto, CancellationToken ct)
    {
        var result = await contentService.UpdateQuestionAsync(id, dto, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id, message = "Question updated successfully." });
    }

    /// <summary>
    /// UC-17.5 Delete Question (Warns if question belongs to an active published exam)
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteQuestion(int id, CancellationToken ct)
    {
        var result = await contentService.DeleteQuestionAsync(id, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}
