using Exam.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Exam.Application.Interfaces;

public interface IExamDbContext
{
    DbSet<PracticeExam> PracticeExams { get; }
    DbSet<Question> Questions { get; }
    DbSet<ExamAttempt> ExamAttempts { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
