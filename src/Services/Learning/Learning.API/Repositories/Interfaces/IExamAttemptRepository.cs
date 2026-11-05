using Learning.API.Entities;

namespace Learning.API.Repositories.Interfaces;

public interface IExamAttemptRepository
{
    Task<bool> ExistsAsync(Guid attemptId, CancellationToken ct = default);
    Task AddAsync(ExamAttemptRecord attemptRecord, CancellationToken ct = default);
    Task<List<ExamAttemptRecord>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<List<ExamAttemptRecord>> GetAllAsync(CancellationToken ct = default);
    Task<List<ExamAttemptRecord>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
    Task<int> CountByUserIdAsync(string userId, CancellationToken ct = default);
    Task<int> GetMaxScoreByUserIdAsync(string userId, CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
    Task<double> GetOverallAverageScoreAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
