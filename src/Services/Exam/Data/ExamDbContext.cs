using Exam.Entities;
using Microsoft.EntityFrameworkCore;

namespace Exam.Data;

public class ExamDbContext(DbContextOptions<ExamDbContext> options) : DbContext(options), IExamDbContext
{
    public DbSet<global::Exam.Entities.Exam> Exams => Set<global::Exam.Entities.Exam>();
    public DbSet<ExamSeries> ExamSeries => Set<ExamSeries>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionGroup> QuestionGroups => Set<QuestionGroup>();
    public DbSet<Passage> Passages => Set<Passage>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
    public DbSet<ExamSessionState> SessionStates => Set<ExamSessionState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExamDbContext).Assembly);
        modelBuilder.Entity<ExamSessionState>().Property(s => s.Revision).IsConcurrencyToken();
        modelBuilder.Entity<ExamSessionState>().ToTable("EXAM_SESSION_STATES");
        modelBuilder.Entity<ExamSessionState>().Property(s => s.Id).HasMaxLength(64);
        modelBuilder.Entity<ExamSessionState>().Property(s => s.UserId).HasMaxLength(450);
        modelBuilder.Entity<ExamSessionState>().HasIndex(s => new { s.UserId, s.StartedAt });
    }
}
