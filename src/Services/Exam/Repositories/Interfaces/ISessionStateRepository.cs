using Exam.Entities;

namespace Exam.Repositories.Interfaces;

public interface ISessionStateRepository
{
    Task<int> CreateAsync(ExamSessionState state, bool practice, int? timeLimit, string parts, CancellationToken ct);
    Task<ExamSessionState?> GetAsync(string key, string userId, CancellationToken ct);
    Task SaveAsync(ExamSessionState state, CancellationToken ct);
    Task<List<ExamSessionState>> GetHistoryAsync(string userId, CancellationToken ct);
    Task CompleteAsync(ExamSessionState state, int listeningCorrect, int readingCorrect,
        int listeningTotal, int readingTotal, CancellationToken ct);
}
