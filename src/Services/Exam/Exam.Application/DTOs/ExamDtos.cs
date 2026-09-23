namespace Exam.Application.DTOs;

public record ExamListItemDto(Guid Id, string Title, string Part, string Description, int TimeLimitMinutes, int QuestionCount);
public record QuestionDto(Guid Id, string Stem, string OptionA, string OptionB, string OptionC, string OptionD, int Order);
public record ExamDetailDto(Guid Id, string Title, string Part, string Description, int TimeLimitMinutes, List<QuestionDto> Questions);
public record SubmitAnswerDto(Guid QuestionId, string Answer);
public record SubmitExamRequest(List<SubmitAnswerDto> Answers);
public record SubmitExamResultDto(Guid AttemptId, int Score, int CorrectAnswers, int TotalQuestions, string ExamTitle);
