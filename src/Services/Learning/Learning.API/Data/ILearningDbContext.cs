using Learning.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Data;

public interface ILearningDbContext
{
    DbSet<Vocabulary> Vocabularies { get; }
    DbSet<VocabPassage> VocabPassages { get; }
    DbSet<SavedVocabulary> SavedVocabularies { get; }
    DbSet<StudyStreak> StudyStreaks { get; }
    DbSet<Achievement> Achievements { get; }
    DbSet<UserAchievement> UserAchievements { get; }
    DbSet<ExamAttemptRecord> ExamAttemptRecords { get; }
    DbSet<UserProfileSnapshot> UserProfiles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
