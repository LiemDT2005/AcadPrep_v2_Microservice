using Exam.Data;
using Exam.Entities;
using Exam.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Repositories;

public class PracticeSessionRepository(IExamDbContext db) : IPracticeSessionRepository
{
    public Task<PracticeSession?> GetActiveAsync(int id, string userId, CancellationToken ct = default) =>
        db.PracticeSessions.AsNoTracking().Include(s => s.Exam)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId && !s.IsSubmitted, ct);

    public async Task AddAsync(PracticeSession session, CancellationToken ct = default)
    {
        db.PracticeSessions.Add(session);
        await db.SaveChangesAsync(ct);
    }
}
