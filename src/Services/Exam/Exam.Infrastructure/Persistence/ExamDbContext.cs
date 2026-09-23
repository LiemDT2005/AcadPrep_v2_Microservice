using Exam.Application.Interfaces;
using Exam.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Exam.Infrastructure.Persistence;

public class ExamDbContext(DbContextOptions<ExamDbContext> options) : DbContext(options), IExamDbContext
{
    public DbSet<PracticeExam> PracticeExams => Set<PracticeExam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PracticeExam>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Questions).WithOne(x => x.PracticeExam!).HasForeignKey(x => x.PracticeExamId);
        });
        modelBuilder.Entity<Question>().HasKey(x => x.Id);
        modelBuilder.Entity<ExamAttempt>().HasKey(x => x.Id);
    }
}
