using BuildingBlocks.Common.Results;
using Microsoft.EntityFrameworkCore;
using Progress.Application.DTOs;
using Progress.Application.Interfaces;
using Progress.Domain.Entities;

namespace Progress.Application.Services;

public class ProgressService(IProgressDbContext db, IProgressCache cache)
{
    public async Task RecordExamCompletedAsync(
        Guid attemptId, string userId, Guid examId, string examTitle,
        int score, int total, int correct, CancellationToken ct = default)
    {
        var exists = await db.LearningRecords.AnyAsync(x => x.AttemptId == attemptId, ct);
        if (exists) return;

        db.LearningRecords.Add(new LearningRecord
        {
            AttemptId = attemptId,
            UserId = userId,
            ExamId = examId,
            ExamTitle = examTitle,
            Score = score,
            TotalQuestions = total,
            CorrectAnswers = correct,
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(userId, ct);
    }

    public async Task<Result<ProgressSummaryDto>> GetSummaryAsync(string userId, CancellationToken ct = default)
    {
        var cached = await cache.GetUserSummaryAsync<ProgressSummaryDto>(userId, ct);
        if (cached is not null) return Result.Success(cached);

        var records = await db.LearningRecords.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CompletedAt)
            .ToListAsync(ct);

        var summary = new ProgressSummaryDto(
            userId,
            records.Count,
            records.Count == 0 ? 0 : Math.Round(records.Average(r => r.Score), 1),
            records.Count == 0 ? 0 : records.Max(r => r.Score),
            records.Take(10).Select(r => new LearningRecordDto(
                r.Id, r.ExamTitle, r.Score, r.CorrectAnswers, r.TotalQuestions, r.CompletedAt)).ToList());

        await cache.SetUserSummaryAsync(userId, summary, ct);
        return Result.Success(summary);
    }
}
