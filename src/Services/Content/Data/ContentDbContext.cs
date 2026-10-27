using Content.Entities;
using Microsoft.EntityFrameworkCore;

namespace Content.Data;

public class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options), IContentDbContext
{
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamSeries> ExamSeries => Set<ExamSeries>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Passage> Passages => Set<Passage>();
    public DbSet<QuestionGroup> QuestionGroups => Set<QuestionGroup>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Exam>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Title).IsRequired().HasMaxLength(250);
            b.Property(e => e.ExamType).HasMaxLength(100);
            b.Property(e => e.AudioUrl).HasMaxLength(500);
            b.Property(e => e.Status).HasConversion<string>();
            b.HasIndex(e => e.Title);
            b.HasIndex(e => e.Status);
            b.HasIndex(e => e.IsDeleted);

            b.HasOne(e => e.ExamSeries)
                .WithMany(s => s.Exams)
                .HasForeignKey(e => e.ExamSeriesId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExamSeries>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(s => s.Name);
        });

        modelBuilder.Entity<Passage>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasOne(p => p.Exam)
                .WithMany(e => e.Passages)
                .HasForeignKey(p => p.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(p => p.QuestionGroup)
                .WithMany(g => g.Passages)
                .HasForeignKey(p => p.QuestionGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuestionGroup>(b =>
        {
            b.HasKey(g => g.Id);
            b.Property(g => g.Name).IsRequired().HasMaxLength(200);
            b.HasOne(g => g.Exam)
                .WithMany(e => e.QuestionGroups)
                .HasForeignKey(g => g.ExamId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Question>(b =>
        {
            b.HasKey(q => q.Id);
            b.Property(q => q.CorrectOption).IsRequired().HasMaxLength(5);

            b.HasOne(q => q.Exam)
                .WithMany(e => e.Questions)
                .HasForeignKey(q => q.ExamId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(q => q.Passage)
                .WithMany(p => p.Questions)
                .HasForeignKey(q => q.PassageId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(q => q.QuestionGroup)
                .WithMany(g => g.Questions)
                .HasForeignKey(q => q.QuestionGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QuestionOption>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.OptionLetter).IsRequired().HasMaxLength(5);
            b.Property(o => o.OptionText).IsRequired().HasMaxLength(1000);

            b.HasOne(o => o.Question)
                .WithMany(q => q.QuestionOptions)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
