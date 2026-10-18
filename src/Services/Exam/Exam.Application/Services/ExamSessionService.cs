using System.Text.Json;
using BuildingBlocks.Common.Results;
using Exam.Application.DTOs;
using Exam.Application.Interfaces;
using Exam.Domain.Entities;
using Exam.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Exam.Application.Services;

public class ExamSessionService(IExamDbContext db)
{
    public async Task<Result<int>> StartFullTestAsync(int examId, string userId, CancellationToken ct = default)
    {
        var exam = await db.Exams.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == examId && !e.IsDeleted && e.Status == ExamStatus.Published, ct);
        if (exam is null)
            return Result<int>.Failure("Exam not found or is not available.");
        if (!await db.Questions.AnyAsync(q => q.ExamId == examId, ct))
            return Result<int>.Failure("This exam has no questions yet.");
        if (exam.Duration <= 0)
            return Result<int>.Failure("This exam has no valid duration.");

        // A start always creates a new attempt. Resumption and submission are separate features.
        var attempt = new ExamAttempt
        {
            ExamId = examId, UserId = userId,
            RemainingTime = checked(exam.Duration * 60), StartedAt = DateTime.UtcNow
        };
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);
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
        if (!await db.Exams.AnyAsync(e => e.Id == examId && !e.IsDeleted && e.Status == ExamStatus.Published, ct))
            return Result<int>.Failure("Exam not found or is not available.");

        var query = db.Questions.AsNoTracking().Where(q => q.ExamId == examId && parts.Contains(q.Part));
        if (tags.Count > 0)
            query = query.Where(q => q.TopicTag != null && tags.Contains(q.TopicTag));
        var questions = await query.OrderBy(q => q.QuestionNumber).ThenBy(q => q.Id)
            .Select(q => new { q.Id, q.Part, q.QuestionNumber, q.QuestionGroupId, q.PassageId }).ToListAsync(ct);
        if (questions.Count == 0)
            return Result<int>.Failure("No questions match your selected parts or tags.");

        // Keep related question sets contiguous, as in the monolith.
        var ids = questions.GroupBy(q => (q.Part, Unit: q.Part is 3 or 4 or 7 && q.QuestionGroupId.HasValue
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
        db.PracticeSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Result<int>.Success(session.Id);
    }

    public async Task<ExamSessionDto?> GetFullTestAsync(int id, string userId, CancellationToken ct = default)
    {
        var attempt = await db.ExamAttempts.AsNoTracking().Include(a => a.Exam)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && !a.IsSubmitted, ct);
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
        var session = await db.PracticeSessions.AsNoTracking().Include(s => s.Exam)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && !s.IsSubmitted, ct);
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
        var query = db.Questions.AsNoTracking().Where(q => q.ExamId == examId);
        if (ids is not null) query = query.Where(q => ids.Contains(q.Id));
        var questions = await query
            .Include(q => q.QuestionOptions).Include(q => q.Passage)
            .Include(q => q.QuestionGroup).ThenInclude(g => g!.Passages)
            .AsSplitQuery().OrderBy(q => q.Part).ThenBy(q => q.QuestionNumber).ThenBy(q => q.Id).ToListAsync(ct);
        return questions.Select(q => new SessionQuestionDto
        {
            Id = q.Id, Part = q.Part, QuestionNumber = q.QuestionNumber, QuestionText = q.QuestionText,
            ImageUrl = q.ImageUrl, PassageId = q.PassageId, QuestionGroupId = q.QuestionGroupId,
            GroupImageUrl = q.QuestionGroup?.ImageUrl,
            Passages = (q.QuestionGroup?.Passages.Count > 0 ? q.QuestionGroup.Passages
                    : q.Passage is not null ? new List<Passage> { q.Passage } : new List<Passage>())
                .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id).Select(p => new SessionPassageDto
                {
                    Id = p.Id, DisplayOrder = p.DisplayOrder, Content = p.Content, ImageUrl = p.ImageUrl
                }).ToList(),
            Options = q.QuestionOptions.OrderBy(o => o.OptionLetter)
                .Select(o => new SessionOptionDto(o.OptionLetter.ToString(), o.OptionText)).ToList()
        }).ToList();
    }
}
