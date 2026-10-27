using System.Security.Claims;
using Learning.Application.DTOs;
using Learning.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Learning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VocabularyController(VocabularyService vocabularyService) : ControllerBase
{
    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) 
        ?? User.FindFirstValue("sub") 
        ?? User.FindFirstValue("UserId") 
        ?? "user-demo-1";

    /// <summary>
    /// UC-11.1: Review Flashcards (Due for spaced repetition review)
    /// </summary>
    [HttpGet("flashcards")]
    public async Task<IActionResult> GetReviewFlashcards(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await vocabularyService.GetReviewFlashcardsAsync(userId, pageNumber, pageSize, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-11.2: Rate Memorization (Spaced Repetition SRS Algorithm)
    /// </summary>
    [HttpPost("rate")]
    public async Task<IActionResult> RateMemorization([FromBody] RateVocabularyRequest request, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await vocabularyService.RateMemorizationAsync(userId, request, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-11.3: Save Vocabulary (Add word to personal notebook)
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> SaveVocabulary([FromBody] SaveVocabularyRequest request, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await vocabularyService.SaveVocabularyAsync(userId, request, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-11.4: View Vocabulary List (Saved notebook with search and pagination)
    /// </summary>
    [HttpGet("notebook")]
    public async Task<IActionResult> GetSavedVocabularies(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await vocabularyService.GetSavedVocabularyListAsync(userId, searchTerm, pageNumber, pageSize, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-11.5: Remove Saved Vocabulary (Remove from personal notebook)
    /// </summary>
    [HttpDelete("{vocabId:int}")]
    public async Task<IActionResult> RemoveSavedVocabulary(int vocabId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var result = await vocabularyService.RemoveSavedVocabularyAsync(userId, vocabId, ct);
        if (!result.IsSuccess) return BadRequest(new { error = result.Error });

        return Ok(new { success = true, message = "Đã xóa từ vựng khỏi sổ tay thành công." });
    }

    /// <summary>
    /// UC-12.1: View Vocabulary Passage (Contextual reading passage of word)
    /// </summary>
    [HttpGet("{vocabId:int}/passages")]
    public async Task<IActionResult> GetVocabularyPassages(int vocabId, CancellationToken ct = default)
    {
        var result = await vocabularyService.GetVocabularyPassagesAsync(vocabId, ct);
        if (!result.IsSuccess) return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>
    /// UC-12.2: Look Up Vocabulary (Dictionary definition, phonetic, example)
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> LookupVocabulary([FromQuery] string keyword, CancellationToken ct = default)
    {
        var result = await vocabularyService.LookupVocabularyAsync(keyword, ct);
        if (!result.IsSuccess) return NotFound(new { error = result.Error });

        return Ok(result.Value);
    }
}
