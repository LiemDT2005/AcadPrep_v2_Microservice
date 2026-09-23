using System.Text.Json;
using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Results;
using BuildingBlocks.EventBus.Abstractions;
using Exam.Application.DTOs;
using Exam.Application.Interfaces;
using Exam.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Exam.Application.Services;

public class ExamService(IExamDbContext db, IEventBus eventBus)
{
    public async Task<Result<List<ExamListItemDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await db.PracticeExams.AsNoTracking()
            .OrderBy(e => e.Title)
            .Select(e => new ExamListItemDto(e.Id, e.Title, e.Part, e.Description, e.TimeLimitMinutes, e.Questions.Count))
            .ToListAsync(ct);
        return Result.Success(items);
    }

    public async Task<Result<ExamDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var exam = await db.PracticeExams.AsNoTracking()
            .Include(e => e.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (exam is null) return Result.Failure<ExamDetailDto>("Exam not found.");

        var dto = new ExamDetailDto(
            exam.Id, exam.Title, exam.Part, exam.Description, exam.TimeLimitMinutes,
            exam.Questions.Select(q => new QuestionDto(q.Id, q.Stem, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.Order)).ToList());
        return Result.Success(dto);
    }

    public async Task<Result<SubmitExamResultDto>> SubmitAsync(Guid examId, string userId, SubmitExamRequest request, CancellationToken ct = default)
    {
        var exam = await db.PracticeExams
            .Include(e => e.Questions)
            .FirstOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null) return Result.Failure<SubmitExamResultDto>("Exam not found.");

        var answerMap = request.Answers.ToDictionary(a => a.QuestionId, a => a.Answer.Trim().ToUpperInvariant());
        var correct = 0;
        foreach (var q in exam.Questions)
        {
            if (answerMap.TryGetValue(q.Id, out var ans) && ans == q.CorrectAnswer.ToUpperInvariant())
                correct++;
        }

        var total = exam.Questions.Count;
        var score = total == 0 ? 0 : (int)Math.Round(correct * 100.0 / total);

        var attempt = new ExamAttempt
        {
            PracticeExamId = examId,
            UserId = userId,
            SubmittedAt = DateTime.UtcNow,
            Score = score,
            CorrectAnswers = correct,
            TotalQuestions = total,
            AnswersJson = JsonSerializer.Serialize(request.Answers)
        };
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        await eventBus.PublishAsync(new ExamCompletedEvent(
            attempt.Id, userId, exam.Id, exam.Title, score, total, correct), ct);

        return Result.Success(new SubmitExamResultDto(attempt.Id, score, correct, total, exam.Title));
    }
}
