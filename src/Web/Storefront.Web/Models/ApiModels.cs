namespace Storefront.Web.Models;

public record AuthResponse(string Token, string Email, string FullName, string UserId, DateTime ExpiresAt);
public record CourseListItem(Guid Id, string Title, string Level, int TargetScore, int LessonCount, string Description);
public record LessonDto(Guid Id, string Title, string Skill, int Order, int DurationMinutes, string Content);
public record CourseDetail(Guid Id, string Title, string Description, string Level, int TargetScore, List<LessonDto> Lessons);
public record ExamListItem(Guid Id, string Title, string Part, string Description, int TimeLimitMinutes, int QuestionCount);
public record QuestionDto(Guid Id, string Stem, string OptionA, string OptionB, string OptionC, string OptionD, int Order);
public record ExamDetail(Guid Id, string Title, string Part, string Description, int TimeLimitMinutes, List<QuestionDto> Questions);
public record SubmitExamResult(Guid AttemptId, int Score, int CorrectAnswers, int TotalQuestions, string ExamTitle);
public record LearningRecordDto(Guid Id, string ExamTitle, int Score, int CorrectAnswers, int TotalQuestions, DateTime CompletedAt);
public record ProgressSummary(string UserId, int TotalAttempts, double AverageScore, int BestScore, List<LearningRecordDto> Recent);
