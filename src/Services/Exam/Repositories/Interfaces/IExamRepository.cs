using ExamEntity = global::Exam.Entities.Exam;

namespace Exam.Repositories.Interfaces;

public interface IExamRepository
{
    Task<ExamEntity?> GetPublishedByIdAsync(int id, CancellationToken ct = default);
}
