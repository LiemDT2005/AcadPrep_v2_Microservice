using Exam.Entities;
using Microsoft.EntityFrameworkCore;

namespace Exam.Data;

public interface IExamDbContext
{
    DbSet<global::Exam.Entities.Exam> Exams { get; }
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
