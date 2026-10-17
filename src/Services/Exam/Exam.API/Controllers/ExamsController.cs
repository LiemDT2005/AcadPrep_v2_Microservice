using BuildingBlocks.Common.Pagination;
using Exam.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Exam.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController(ExamService examService) : ControllerBase
{
    /// <summary>
    /// Get list of exams (with pagination, search, and filtering by series, year) - UC-3.1
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? search,
        [FromQuery] string? seriesName,
        [FromQuery] int? year,
        [FromQuery] int pageIndex = PaginationConstants.DefaultPageIndex,
        [FromQuery] int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await examService.GetExamListAsync(search, seriesName, year, pageIndex, pageSize, ct);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get exam detail along with parts breakdown and attempt history - UC-3.2
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(
        [FromRoute] int id,
        [FromQuery] int? userId,
        CancellationToken ct = default)
    {
        var result = await examService.GetExamDetailAsync(id, userId, ct);
        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
