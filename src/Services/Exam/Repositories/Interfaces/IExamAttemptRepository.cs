using Exam.Entities;

namespace Exam.Repositories.Interfaces;

public interface IExamAttemptRepository
{
    Task<int> CountForExamAsync(int examId, CancellationToken ct = default);
    Task<List<ExamAttempt>> GetHistoryAsync(int examId, string userId, CancellationToken ct = default);
    Task<ExamAttempt?> GetActiveAsync(int id, string userId, CancellationToken ct = default);
    Task AddAsync(ExamAttempt attempt, CancellationToken ct = default);
}
