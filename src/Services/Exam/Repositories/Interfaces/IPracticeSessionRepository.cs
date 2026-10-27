using Exam.Entities;

namespace Exam.Repositories.Interfaces;

public interface IPracticeSessionRepository
{
    Task<PracticeSession?> GetActiveAsync(int id, string userId, CancellationToken ct = default);
    Task AddAsync(PracticeSession session, CancellationToken ct = default);
}
