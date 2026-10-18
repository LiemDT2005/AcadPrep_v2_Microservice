using Exam.Application.Interfaces;
using Exam.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Persistence;

public class ExamDbContext(DbContextOptions<ExamDbContext> options) : DbContext(options), IExamDbContext
{
    public DbSet<Domain.Entities.Exam> Exams => Set<Domain.Entities.Exam>();
    public DbSet<ExamSeries> ExamSeries => Set<ExamSeries>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionGroup> QuestionGroups => Set<QuestionGroup>();
    public DbSet<Passage> Passages => Set<Passage>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExamDbContext).Assembly);
    }
}
