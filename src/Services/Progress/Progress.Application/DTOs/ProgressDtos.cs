namespace Progress.Application.DTOs;

public record LearningRecordDto(Guid Id, string ExamTitle, int Score, int CorrectAnswers, int TotalQuestions, DateTime CompletedAt);
public record ProgressSummaryDto(string UserId, int TotalAttempts, double AverageScore, int BestScore, List<LearningRecordDto> Recent);
