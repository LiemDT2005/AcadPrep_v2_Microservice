using Exam.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Exam.Application.Interfaces;

public interface IExamDbContext
{
    DbSet<Domain.Entities.Exam> Exams { get; }
    DbSet<ExamSeries> ExamSeries { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionGroup> QuestionGroups { get; }
    DbSet<Passage> Passages { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<ExamAttempt> ExamAttempts { get; }
    DbSet<AttemptAnswer> AttemptAnswers { get; }
    DbSet<PracticeSession> PracticeSessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
