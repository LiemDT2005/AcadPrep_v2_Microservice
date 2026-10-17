using Exam.Application.Common.Models;

namespace Exam.Application.DTOs;

public class GetExamDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? CoverImageUrl { get; set; }
    public int QuestionCount { get; set; }
    public int AttemptCount { get; set; }
}

public class GetExamListResponse
{
    public PaginatedList<GetExamDto> Exams { get; set; } = null!;
    public List<string> SeriesFilters { get; set; } = new();
    public List<string> YearFilters { get; set; } = new();
}

public class GetExamDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Duration { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? CoverImageUrl { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalAttempts { get; set; }
    public List<PartSummaryDto> Parts { get; set; } = new();
    public List<UserAttemptHistoryDto> AttemptHistory { get; set; } = new();
}

public class PartSummaryDto
{
    public int PartNumber { get; set; }
    public string PartName { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class UserAttemptHistoryDto
{
    public int AttemptId { get; set; }
    public int ListeningScore { get; set; }
    public int ReadingScore { get; set; }
    public int TotalScore { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RemainingTime { get; set; }
    public bool IsSubmitted { get; set; }
}
