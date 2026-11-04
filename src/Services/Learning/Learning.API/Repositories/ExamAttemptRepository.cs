using Learning.API.Data;
using Learning.API.Entities;
using Learning.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Repositories;

public class ExamAttemptRepository(LearningDbContext db) : IExamAttemptRepository
{
    public async Task<bool> ExistsAsync(Guid attemptId, CancellationToken ct = default)
    {
        return await db.ExamAttemptRecords.AnyAsync(x => x.AttemptId == attemptId, ct);
    }

    public async Task AddAsync(ExamAttemptRecord attemptRecord, CancellationToken ct = default)
    {
        await db.ExamAttemptRecords.AddAsync(attemptRecord, ct);
    }

    public async Task<List<ExamAttemptRecord>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await db.ExamAttemptRecords.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<List<ExamAttemptRecord>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.ExamAttemptRecords.AsNoTracking().ToListAsync(ct);
    }

    public async Task<List<ExamAttemptRecord>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        return await db.ExamAttemptRecords.AsNoTracking()
            .Where(ea => ea.SubmittedAt >= startDate && ea.SubmittedAt <= endDate)
            .ToListAsync(ct);
    }

    public async Task<int> CountByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await db.ExamAttemptRecords
            .Where(x => x.UserId == userId)
            .CountAsync(ct);
    }

    public async Task<int> GetMaxScoreByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await db.ExamAttemptRecords
            .Where(x => x.UserId == userId)
            .MaxAsync(x => (int?)x.Score, ct) ?? 0;
    }

    public async Task<int> CountAllAsync(CancellationToken ct = default)
    {
        return await db.ExamAttemptRecords.CountAsync(ct);
    }

    public async Task<double> GetOverallAverageScoreAsync(CancellationToken ct = default)
    {
        return await db.ExamAttemptRecords.AverageAsync(ea => (double)ea.Score, ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await db.SaveChangesAsync(ct);
    }
}
