using Learning.Application.Interfaces;
using Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure.Persistence;

public class LearningDbContext(DbContextOptions<LearningDbContext> options)
    : DbContext(options), ILearningDbContext
{
    public DbSet<Vocabulary> Vocabularies => Set<Vocabulary>();
    public DbSet<VocabPassage> VocabPassages => Set<VocabPassage>();
    public DbSet<SavedVocabulary> SavedVocabularies => Set<SavedVocabulary>();
    public DbSet<StudyStreak> StudyStreaks => Set<StudyStreak>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<ExamAttemptRecord> ExamAttemptRecords => Set<ExamAttemptRecord>();
    public DbSet<UserProfileSnapshot> UserProfiles => Set<UserProfileSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Vocabulary
        modelBuilder.Entity<Vocabulary>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Word).IsRequired().HasMaxLength(200);
            b.Property(x => x.Meaning).IsRequired().HasMaxLength(1000);
            b.HasMany(x => x.Passages).WithOne(x => x.Vocabulary).HasForeignKey(x => x.VocabularyId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.SavedVocabularies).WithOne(x => x.Vocabulary).HasForeignKey(x => x.VocabularyId).OnDelete(DeleteBehavior.Cascade);
        });

        // VocabPassage
        modelBuilder.Entity<VocabPassage>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Content).IsRequired();
        });

        // SavedVocabulary
        modelBuilder.Entity<SavedVocabulary>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.UserId, x.VocabularyId }).IsUnique();
        });

        // StudyStreak
        modelBuilder.Entity<StudyStreak>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.UserId).IsUnique();
        });

        // Achievement
        modelBuilder.Entity<Achievement>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).IsRequired().HasMaxLength(500);
        });

        // UserAchievement
        modelBuilder.Entity<UserAchievement>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.UserId, x.AchievementId }).IsUnique();
            b.HasOne(x => x.Achievement).WithMany(x => x.UserAchievements).HasForeignKey(x => x.AchievementId).OnDelete(DeleteBehavior.Cascade);
        });

        // ExamAttemptRecord
        modelBuilder.Entity<ExamAttemptRecord>(b =>
        {
            b.HasKey(x => x.AttemptId);
            b.HasIndex(x => x.UserId);
        });

        // UserProfileSnapshot
        modelBuilder.Entity<UserProfileSnapshot>(b =>
        {
            b.HasKey(x => x.UserId);
        });
    }
}
