namespace Storefront.Web.Models;

public class StartPracticeRequest
{
    public List<int> SelectedPartNumbers { get; set; } = [];
    public List<string> SelectedTags { get; set; } = [];
    public int? TimeLimitMinutes { get; set; }
}

public class ExamSessionDto
{
    public int SessionId { get; set; }
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<SessionQuestionDto> Questions { get; set; } = [];
}

// Deliberately excludes correct answers, explanations and audio playback data.
public class SessionQuestionDto
{
    public int Id { get; set; }
    public int QuestionNumber { get; set; }
    public int Part { get; set; }
    public string? QuestionText { get; set; }
    public string? ImageUrl { get; set; }
    public int? QuestionGroupId { get; set; }
    public int? PassageId { get; set; }
    public string? GroupImageUrl { get; set; }
    public List<SessionPassageDto> Passages { get; set; } = [];
    public List<SessionOptionDto> Options { get; set; } = [];
}

public class SessionPassageDto
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
}

public record SessionOptionDto(string Letter, string Text);
