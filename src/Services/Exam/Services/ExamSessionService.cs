using System.Text.Json;
using AutoMapper;
using BuildingBlocks.Common.Results;
using Exam.DTOs;
using Exam.Entities;
using Exam.Repositories.Interfaces;
using Exam.Services.Interfaces;

namespace Exam.Services;

public class ExamSessionService(
    IExamRepository exams,
    IQuestionRepository questions,
    IExamAttemptRepository attempts,
    IPracticeSessionRepository practices,
    IMapper mapper) : IExamSessionService
{
    public async Task<Result<int>> StartFullTestAsync(int examId, string userId, CancellationToken ct = default)
    {
        var exam = await exams.GetPublishedByIdAsync(examId, ct);
        if (exam is null)
            return Result<int>.Failure("Exam not found or is not available.");
        if (!await questions.AnyForExamAsync(examId, ct))
            return Result<int>.Failure("This exam has no questions yet.");
        if (exam.Duration <= 0)
            return Result<int>.Failure("This exam has no valid duration.");

        // A start always creates a new attempt. Resumption and submission are separate features.
        var attempt = new ExamAttempt
        {
            ExamId = examId, UserId = userId,
            RemainingTime = checked(exam.Duration * 60), StartedAt = DateTime.UtcNow
        };
        await attempts.AddAsync(attempt, ct);
        return Result<int>.Success(attempt.Id);
    }

    public async Task<Result<int>> StartPracticeAsync(int examId, string userId, StartPracticeRequest request, CancellationToken ct = default)
    {
        if (request.SelectedPartNumbers is not { Count: > 0 } || request.SelectedPartNumbers.Any(p => p is < 1 or > 7))
            return Result<int>.Failure("Choose at least one valid Part (1–7).");
        if (request.TimeLimitMinutes is <= 0 or > 120)
            return Result<int>.Failure("Time limit must be between 1 and 120 minutes, or null for no limit.");
        if (request.SelectedTags is null || request.SelectedTags.Any(string.IsNullOrWhiteSpace))
            return Result<int>.Failure("Topic tags must not be empty.");
        var parts = request.SelectedPartNumbers.Distinct().Order().ToList();
        var tags = request.SelectedTags.Select(t => t.Trim()).Distinct().ToList();
        if (string.Join(',', tags).Length > 500)
            return Result<int>.Failure("Too many topic tags selected.");
        if (await exams.GetPublishedByIdAsync(examId, ct) is null)
            return Result<int>.Failure("Exam not found or is not available.");

        var candidates = await questions.GetPracticeCandidatesAsync(examId, parts, tags, ct);
        if (candidates.Count == 0)
            return Result<int>.Failure("No questions match your selected parts or tags.");

        // Keep related question sets contiguous, as in the monolith.
        var ids = candidates.GroupBy(q => (q.Part, Unit: q.Part is 3 or 4 or 7 && q.QuestionGroupId.HasValue
                ? $"group:{q.QuestionGroupId}" : q.Part == 6 && q.PassageId.HasValue
                    ? $"passage:{q.PassageId}" : $"question:{q.Id}"))
            .OrderBy(g => g.Key.Part).ThenBy(g => g.Min(q => q.QuestionNumber))
            .SelectMany(g => g.OrderBy(q => q.QuestionNumber).Select(q => q.Id)).ToList();
        var session = new PracticeSession
        {
            ExamId = examId, UserId = userId, SelectedParts = string.Join(',', parts),
            SelectedTags = string.Join(',', tags), TimeLimit = request.TimeLimitMinutes,
            CombinedQuestionsList = JsonSerializer.Serialize(ids), TotalQuestions = ids.Count,
            CreatedAt = DateTime.UtcNow
        };
        await practices.AddAsync(session, ct);
        return Result<int>.Success(session.Id);
    }

    public async Task<ExamSessionDto?> GetFullTestAsync(int id, string userId, CancellationToken ct = default)
    {
        var attempt = await attempts.GetActiveAsync(id, userId, ct);
        if (attempt is null) return null;
        return new ExamSessionDto
        {
            SessionId = id, ExamId = attempt.ExamId, ExamTitle = attempt.Exam.Title, Mode = "full-test",
            StartedAt = DateTime.SpecifyKind(attempt.StartedAt, DateTimeKind.Utc),
            ExpiresAt = DateTime.SpecifyKind(attempt.StartedAt, DateTimeKind.Utc).AddSeconds(attempt.RemainingTime),
            Questions = await LoadQuestionsAsync(attempt.ExamId, null, ct)
        };
    }

    public async Task<ExamSessionDto?> GetPracticeAsync(int id, string userId, CancellationToken ct = default)
    {
        var session = await practices.GetActiveAsync(id, userId, ct);
        if (session is null) return null;
        var ids = JsonSerializer.Deserialize<List<int>>(session.CombinedQuestionsList) ?? [];
        var questions = await LoadQuestionsAsync(session.ExamId, ids, ct);
        var order = ids.Select((questionId, index) => (questionId, index)).ToDictionary(x => x.questionId, x => x.index);
        return new ExamSessionDto
        {
            SessionId = id, ExamId = session.ExamId, ExamTitle = session.Exam.Title, Mode = "practice",
            StartedAt = DateTime.SpecifyKind(session.CreatedAt, DateTimeKind.Utc),
            ExpiresAt = session.TimeLimit.HasValue ? DateTime.SpecifyKind(session.CreatedAt, DateTimeKind.Utc).AddMinutes(session.TimeLimit.Value) : null,
            Questions = questions.OrderBy(q => order[q.Id]).ToList()
        };
    }

    private async Task<List<SessionQuestionDto>> LoadQuestionsAsync(int examId, List<int>? ids, CancellationToken ct)
    {
        var entities = await questions.GetSessionQuestionsAsync(examId, ids, ct);
        return mapper.Map<List<SessionQuestionDto>>(entities);
    }
}
