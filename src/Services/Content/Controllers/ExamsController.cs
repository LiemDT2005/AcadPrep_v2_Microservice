using Content.DTOs;
using Content.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Content.Controllers;

[Authorize(Roles = "Admin,Moderator")]
[ApiController]
[Route("api/cms/[controller]")]
public class ExamsController(IContentService contentService) : ControllerBase
{
    /// <summary>
    /// UC-16.1 View Exams / UC-16.6 Search / UC-16.7 Filter with Pagination
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetExams(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] string? examType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await contentService.GetPagedExamsAsync(keyword, status, examType, page, pageSize, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-16.3 View Exam Detail & Statistics
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetExamById(int id, CancellationToken ct)
    {
        var result = await contentService.GetExamByIdAsync(id, ct);
        if (!result.IsSuccess)
            return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-16.2 Create Exam
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateExam([FromBody] CreateExamDto dto, CancellationToken ct)
    {
        var result = await contentService.CreateExamAsync(dto, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetExamById), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>
    /// UC-16.4 Update Exam Metadata
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateExam(int id, [FromBody] UpdateExamDto dto, CancellationToken ct)
    {
        var result = await contentService.UpdateExamAsync(id, dto, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { success = true, message = "Exam updated successfully." });
    }

    /// <summary>
    /// UC-16.4 Publish / Unpublish Exam
    /// </summary>
    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusRequest req, CancellationToken ct)
    {
        var result = await contentService.ChangeStatusAsync(id, req.Status, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { success = true, message = $"Status changed to {req.Status}." });
    }

    /// <summary>
    /// UC-16.5 Delete Exam (Soft delete if attempts > 0, hard delete if 0 attempts)
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteExam(int id, CancellationToken ct)
    {
        var result = await contentService.DeleteExamAsync(id, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Restore Soft-Deleted Exam
    /// </summary>
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> RestoreExam(int id, CancellationToken ct)
    {
        var result = await contentService.RestoreExamAsync(id, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { success = true, message = "Exam restored and published." });
    }

    /// <summary>
    /// Get Exam Aggregate Statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetExamStats(CancellationToken ct)
    {
        var result = await contentService.GetExamStatsAsync(ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get Available Exam Series
    /// </summary>
    [HttpGet("series")]
    public async Task<IActionResult> GetExamSeries(CancellationToken ct)
    {
        var result = await contentService.GetExamSeriesAsync(ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    // ==========================================
    // EXAM CONTENT BUILDERS (UC-17.2, 17.4, 17.5)
    // ==========================================

    [HttpPost("{examId:int}/content/listening-question")]
    public async Task<IActionResult> SaveListeningQuestion(int examId, [FromBody] SaveListeningQuestionRequestDto dto, CancellationToken ct)
    {
        var req = dto with { ExamId = examId };
        var result = await contentService.SaveListeningQuestionAsync(req, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id = result.Value, message = "Listening question saved." });
    }

    [HttpPost("{examId:int}/content/listening-group")]
    public async Task<IActionResult> SaveListeningGroup(int examId, [FromBody] SaveListeningGroupRequestDto dto, CancellationToken ct)
    {
        var req = dto with { ExamId = examId };
        var result = await contentService.SaveListeningGroupAsync(req, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id = result.Value, message = "Listening group saved." });
    }

    [HttpPost("{examId:int}/content/part5")]
    public async Task<IActionResult> SavePart5Question(int examId, [FromBody] SavePart5QuestionRequestDto dto, CancellationToken ct)
    {
        var req = dto with { ExamId = examId };
        var result = await contentService.SavePart5QuestionAsync(req, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id = result.Value, message = "Part 5 question saved." });
    }

    [HttpPost("{examId:int}/content/part6")]
    public async Task<IActionResult> SavePart6Set(int examId, [FromBody] SavePart6SetRequestDto dto, CancellationToken ct)
    {
        var req = dto with { ExamId = examId };
        var result = await contentService.SavePart6SetAsync(req, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id = result.Value, message = "Part 6 passage & questions saved." });
    }

    [HttpPost("{examId:int}/content/part7")]
    public async Task<IActionResult> SavePart7Set(int examId, [FromBody] SavePart7SetRequestDto dto, CancellationToken ct)
    {
        var req = dto with { ExamId = examId };
        var result = await contentService.SavePart7SetAsync(req, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id = result.Value, message = "Part 7 reading set saved." });
    }

    [HttpDelete("{examId:int}/content/{contentType}/{targetId:int}")]
    public async Task<IActionResult> DeleteExamContent(int examId, string contentType, int targetId, CancellationToken ct)
    {
        var result = await contentService.DeleteExamContentAsync(examId, contentType, targetId, ct);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}

public record ChangeStatusRequest(string Status);
