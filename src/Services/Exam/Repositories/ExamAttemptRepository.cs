using Exam.Data;
using Exam.Entities;
using Exam.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Repositories;

public class ExamAttemptRepository(IExamDbContext db) : IExamAttemptRepository
{
    public async Task<int> CountForExamAsync(int examId, CancellationToken ct = default)
    {
        if (db is not ExamDbContext runtime) return await db.ExamAttempts.CountAsync(a => a.ExamId == examId, ct);
        var all = await runtime.SessionStates.AsNoTracking().Where(s => s.Id.StartsWith("full-test:")).Select(s => new { s.Id, s.ExamId }).ToListAsync(ct);
        var ids = all.Select(s => int.Parse(s.Id.Split(':')[1])).ToList();
        return all.Count(s => s.ExamId == examId) + await db.ExamAttempts.CountAsync(a => a.ExamId == examId && !ids.Contains(a.Id), ct);
    }

    public async Task<List<ExamAttempt>> GetHistoryAsync(int examId, string userId, CancellationToken ct = default)
    {
        var legacy = await db.ExamAttempts.AsNoTracking().Where(a => a.ExamId == examId && a.UserId == userId)
            .OrderByDescending(a => a.StartedAt).ToListAsync(ct);
        if (db is not ExamDbContext runtime) return legacy;
        var all = await runtime.SessionStates.AsNoTracking().Where(s => s.UserId == userId && s.Id.StartsWith("full-test:")).ToListAsync(ct);
        var ids = all.Select(s => int.Parse(s.Id.Split(':')[1])).ToHashSet();
        return legacy.Where(a => !ids.Contains(a.Id)).Concat(all.Where(s => s.ExamId == examId).Select(s => new ExamAttempt
        {
            Id = int.Parse(s.Id.Split(':')[1]), ExamId = s.ExamId, UserId = s.UserId, StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt, IsSubmitted = s.IsSubmitted, ListeningScore = s.ListeningScore,
            ReadingScore = s.ReadingScore, TotalScore = s.ListeningScore + s.ReadingScore
        })).OrderByDescending(a => a.StartedAt).ToList();
    }

    public Task<ExamAttempt?> GetActiveAsync(int id, string userId, CancellationToken ct = default) =>
        db.ExamAttempts.AsNoTracking().Include(a => a.Exam)
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && !a.IsSubmitted, ct);

    public async Task AddAsync(ExamAttempt attempt, CancellationToken ct = default)
    {
        db.ExamAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);
    }
}
