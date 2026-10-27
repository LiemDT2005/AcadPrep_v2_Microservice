using Exam.Data;
using Exam.Entities.Enums;
using Exam.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ExamEntity = global::Exam.Entities.Exam;

namespace Exam.Repositories;

public class ExamRepository(IExamDbContext db) : IExamRepository
{
    public Task<ExamEntity?> GetPublishedByIdAsync(int id, CancellationToken ct = default) =>
        db.Exams.AsNoTracking().Include(e => e.ExamSeries)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted && e.Status == ExamStatus.Published, ct);
}
