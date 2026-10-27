using Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure.Persistence;

public static class LearningSeeder
{
    public static async Task SeedAsync(LearningDbContext db)
    {
        // 1. Seed Achievements
        if (!await db.Achievements.AnyAsync())
        {
            db.Achievements.AddRange(
                new Achievement { Name = "First Blood", Description = "Complete your first exam", IconUrl = "https://cdn-icons-png.flaticon.com/512/1000/1000185.png", ConditionType = "ExamsCompleted", ConditionValue = 1 },
                new Achievement { Name = "Perfect Score", Description = "Get a perfect score on any exam", IconUrl = "https://cdn-icons-png.flaticon.com/512/1000/1000184.png", ConditionType = "Score", ConditionValue = 990 },
                new Achievement { Name = "Streak Master", Description = "Maintain a 7-day study streak", IconUrl = "https://cdn-icons-png.flaticon.com/512/4302/4302196.png", ConditionType = "Streak", ConditionValue = 7 },
                new Achievement { Name = "Word Collector", Description = "Save 50 vocabularies", IconUrl = "https://cdn-icons-png.flaticon.com/512/2232/2232688.png", ConditionType = "VocabCount", ConditionValue = 50 },
                new Achievement { Name = "Speed Demon", Description = "Complete a full test in under 90 minutes", IconUrl = "https://cdn-icons-png.flaticon.com/512/3524/3524388.png", ConditionType = "Speed", ConditionValue = 90 }
            );
            await db.SaveChangesAsync();
        }

        // 2. Seed Vocabularies & Passages
        if (!await db.Vocabularies.AnyAsync())
        {
            var vocabs = new List<Vocabulary>
            {
                new() {
                    Word = "Accommodate", Phonetic = "/ə'kɑːmədeɪt/",
                    Meaning = "To provide lodging or sufficient space / Cung cấp chỗ ở, đáp ứng nhu cầu",
                    Example = "The hotel can accommodate up to 500 guests.",
                    Passages = [
                        new() { Content = "The Grand Horizon Convention Center was specially renovated to accommodate large-scale international summits and symposiums." }
                    ]
                },
                new() {
                    Word = "Comprehensive", Phonetic = "/ˌkɑːmprɪ'hensɪv/",
                    Meaning = "Complete; including all or nearly all elements / Toàn diện, bao quát",
                    Example = "We offer a comprehensive training program.",
                    Passages = [
                        new() { Content = "Our human resources department has introduced a comprehensive orientation program designed to help newly onboarded associates adapt quickly." }
                    ]
                },
                new() {
                    Word = "Implement", Phonetic = "/'ɪmplɪment/",
                    Meaning = "To put into effect / Triển khai thực hiện",
                    Example = "The changes to the system will be implemented next year.",
                    Passages = [
                        new() { Content = "Management has decided to implement the revised safety protocol starting from the commencement of the third quarter." }
                    ]
                },
                new() {
                    Word = "Crucial", Phonetic = "/'kruːʃl/",
                    Meaning = "Decisive or critical, especially in the success of something / Cốt yếu, quyết định",
                    Example = "It is crucial that we arrive before 8 o'clock.",
                    Passages = [
                        new() { Content = "Timely communication between field engineers and the central dispatch is crucial during emergency restoration operations." }
                    ]
                },
                new() {
                    Word = "Substantial", Phonetic = "/səb'stænʃl/",
                    Meaning = "Of considerable importance, size, or worth / Đáng kể, quan trọng",
                    Example = "She inherited a substantial fortune from her grandmother.",
                    Passages = [
                        new() { Content = "Following the advertising campaign, the retail division observed a substantial surge in quarterly operating revenue." }
                    ]
                },
                new() {
                    Word = "Negotiate", Phonetic = "/nɪ'goʊʃieɪt/",
                    Meaning = "Try to reach an agreement or compromise / Đàm phán, thương lượng",
                    Example = "We managed to negotiate a good deal.",
                    Passages = [
                        new() { Content = "The procurement committee is actively negotiating terms with local logistics suppliers to mitigate cross-border transit delays." }
                    ]
                },
                new() {
                    Word = "Perspective", Phonetic = "/pər'spektɪv/",
                    Meaning = "A particular attitude toward or way of regarding something / Góc nhìn, quan điểm",
                    Example = "Try to see the issue from a different perspective.",
                    Passages = [
                        new() { Content = "From the perspective of long-term sustainable growth, allocating surplus budget to employee development remains paramount." }
                    ]
                },
                new() {
                    Word = "Collaborate", Phonetic = "/kə'læbəreɪt/",
                    Meaning = "Work jointly on an activity or project / Hợp tác, cộng tác",
                    Example = "We need to collaborate with other teams on this project.",
                    Passages = [
                        new() { Content = "The software division collaborated closely with UI consultants to overhaul the digital portal's user journey." }
                    ]
                },
                new() {
                    Word = "Resilient", Phonetic = "/rɪ'zɪliənt/",
                    Meaning = "Able to withstand or recover quickly from difficult conditions / Kiên cường, phục hồi nhanh",
                    Example = "She is a resilient person who always bounces back from difficulties.",
                    Passages = [
                        new() { Content = "Our cloud infrastructure is resilient to unexpected network fluctuations thanks to automated regional load-balancing." }
                    ]
                },
                new() {
                    Word = "Preliminary", Phonetic = "/prɪ'lɪmɪneri/",
                    Meaning = "Preceding or done in preparation / Sơ bộ, chuẩn bị",
                    Example = "The preliminary results look promising.",
                    Passages = [
                        new() { Content = "According to the preliminary audit report, the financial discrepancies stemmed from manual entry oversights rather than system bugs." }
                    ]
                }
            };
            db.Vocabularies.AddRange(vocabs);
            await db.SaveChangesAsync();
        }

        // 3. Seed User Profiles Snapshot
        if (!await db.UserProfiles.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.UserProfiles.AddRange(
                new UserProfileSnapshot { UserId = "user-demo-1", FullName = "Hana Nguyen", Email = "hana@acadprep.com", CreatedAt = now.AddDays(-60), IsActive = true },
                new UserProfileSnapshot { UserId = "user-demo-2", FullName = "Minh Tran", Email = "minh@acadprep.com", CreatedAt = now.AddDays(-45), IsActive = true },
                new UserProfileSnapshot { UserId = "user-demo-3", FullName = "Hoang Nguyen", Email = "hoang@acadprep.com", CreatedAt = now.AddDays(-30), IsActive = true },
                new UserProfileSnapshot { UserId = "user-demo-4", FullName = "Lan Anh", Email = "lananh@acadprep.com", CreatedAt = now.AddDays(-25), IsActive = true },
                new UserProfileSnapshot { UserId = "user-demo-5", FullName = "Duy Liem", Email = "duyliem@acadprep.com", CreatedAt = now.AddDays(-20), IsActive = true },
                new UserProfileSnapshot { UserId = "user-demo-6", FullName = "Tuan Anh", Email = "tuananh@acadprep.com", CreatedAt = now.AddDays(-15), IsActive = true }
            );
            await db.SaveChangesAsync();
        }

        // 4. Seed StudyStreaks
        if (!await db.StudyStreaks.AnyAsync())
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            db.StudyStreaks.AddRange(
                new StudyStreak { UserId = "user-demo-1", CurrentStreak = 12, MaxStreak = 15, LastActiveDate = today },
                new StudyStreak { UserId = "user-demo-2", CurrentStreak = 8, MaxStreak = 10, LastActiveDate = today },
                new StudyStreak { UserId = "user-demo-3", CurrentStreak = 5, MaxStreak = 6, LastActiveDate = today.AddDays(-1) },
                new StudyStreak { UserId = "user-demo-4", CurrentStreak = 3, MaxStreak = 5, LastActiveDate = today },
                new StudyStreak { UserId = "user-demo-5", CurrentStreak = 1, MaxStreak = 2, LastActiveDate = today.AddDays(-2) }
            );
            await db.SaveChangesAsync();
        }

        // 5. Seed ExamAttemptRecords (for Leaderboard, Performance Analysis, Score Progress, and Admin Reports)
        if (!await db.ExamAttemptRecords.AnyAsync())
        {
            var examId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var examId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var examId3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

            var attempts = new List<ExamAttemptRecord>
            {
                // user-demo-1: 3 attempts showing clear score progression (580 -> 690 -> 785)
                new() {
                    AttemptId = Guid.NewGuid(), UserId = "user-demo-1", ExamId = examId1,
                    ExamTitle = "ETS TOEIC 2025 Test 1", Score = 580, ListeningScore = 310, ReadingScore = 270,
                    TotalQuestions = 200, CorrectAnswers = 120, ListeningCorrect = 65, ReadingCorrect = 55,
                    PartMasteryJson = "{\"1\":85,\"2\":75,\"3\":60,\"4\":55,\"5\":65,\"6\":50,\"7\":45}",
                    SubmittedAt = DateTime.UtcNow.AddDays(-20)
                },
                new() {
                    AttemptId = Guid.NewGuid(), UserId = "user-demo-1", ExamId = examId2,
                    ExamTitle = "ETS TOEIC 2025 Test 2", Score = 690, ListeningScore = 365, ReadingScore = 325,
                    TotalQuestions = 200, CorrectAnswers = 142, ListeningCorrect = 76, ReadingCorrect = 66,
                    PartMasteryJson = "{\"1\":90,\"2\":80,\"3\":70,\"4\":65,\"5\":75,\"6\":60,\"7\":55}",
                    SubmittedAt = DateTime.UtcNow.AddDays(-10)
                },
                new() {
                    AttemptId = Guid.NewGuid(), UserId = "user-demo-1", ExamId = examId3,
                    ExamTitle = "ETS TOEIC 2025 Test 3", Score = 785, ListeningScore = 415, ReadingScore = 370,
                    TotalQuestions = 200, CorrectAnswers = 162, ListeningCorrect = 86, ReadingCorrect = 76,
                    PartMasteryJson = "{\"1\":95,\"2\":85,\"3\":80,\"4\":75,\"5\":80,\"6\":70,\"7\":65}",
                    SubmittedAt = DateTime.UtcNow.AddDays(-2)
                },

                // user-demo-2: 2 attempts
                new() {
                    AttemptId = Guid.NewGuid(), UserId = "user-demo-2", ExamId = examId1,
                    ExamTitle = "ETS TOEIC 2025 Test 1", Score = 650, ListeningScore = 340, ReadingScore = 310,
                    TotalQuestions = 200, CorrectAnswers = 135, ListeningCorrect = 70, ReadingCorrect = 65,
                    PartMasteryJson = "{\"1\":80,\"2\":70,\"3\":65,\"4\":60,\"5\":70,\"6\":60,\"7\":50}",
                    SubmittedAt = DateTime.UtcNow.AddDays(-15)
                },
                new() {
                    AttemptId = Guid.NewGuid(), UserId = "user-demo-2", ExamId = examId2,
                    ExamTitle = "ETS TOEIC 2025 Test 2", Score = 720, ListeningScore = 380, ReadingScore = 340,
                    TotalQuestions = 200, CorrectAnswers = 150, ListeningCorrect = 78, ReadingCorrect = 72,
                    PartMasteryJson = "{\"1\":85,\"2\":75,\"3\":70,\"4\":65,\"5\":75,\"6\":65,\"7\":60}",
                    SubmittedAt = DateTime.UtcNow.AddDays(-5)
                },

                // user-demo-3: 1 attempt
                new() {
                    AttemptId = Guid.NewGuid(), UserId = "user-demo-3", ExamId = examId1,
                    ExamTitle = "ETS TOEIC 2025 Test 1", Score = 510, ListeningScore = 270, ReadingScore = 240,
                    TotalQuestions = 200, CorrectAnswers = 105, ListeningCorrect = 55, ReadingCorrect = 50,
                    PartMasteryJson = "{\"1\":70,\"2\":60,\"3\":50,\"4\":45,\"5\":55,\"6\":45,\"7\":35}",
                    SubmittedAt = DateTime.UtcNow.AddDays(-8)
                }
            };
            db.ExamAttemptRecords.AddRange(attempts);
            await db.SaveChangesAsync();
        }

        // 6. Seed SavedVocabularies for user-demo-1
        if (!await db.SavedVocabularies.AnyAsync())
        {
            var seededVocabs = await db.Vocabularies.OrderBy(v => v.Id).Take(5).ToListAsync();
            if (seededVocabs.Count >= 5)
            {
                var now = DateTime.UtcNow;
                db.SavedVocabularies.AddRange(
                    new SavedVocabulary { UserId = "user-demo-1", VocabularyId = seededVocabs[0].Id, Interval = 1, DateSaved = now.AddDays(-5), NextReviewDate = now.AddDays(-1) }, // Due
                    new SavedVocabulary { UserId = "user-demo-1", VocabularyId = seededVocabs[1].Id, Interval = 1, DateSaved = now.AddDays(-4), NextReviewDate = now.AddDays(-1) }, // Due
                    new SavedVocabulary { UserId = "user-demo-1", VocabularyId = seededVocabs[2].Id, Interval = 2, DateSaved = now.AddDays(-3), NextReviewDate = now.AddDays(1) },
                    new SavedVocabulary { UserId = "user-demo-1", VocabularyId = seededVocabs[3].Id, Interval = 4, DateSaved = now.AddDays(-2), NextReviewDate = now.AddDays(2) },
                    new SavedVocabulary { UserId = "user-demo-1", VocabularyId = seededVocabs[4].Id, Interval = 1, DateSaved = now.AddDays(-1), NextReviewDate = now.AddDays(1) }
                );
                await db.SaveChangesAsync();
            }
        }

        // 7. Seed UserAchievements for user-demo-1
        if (!await db.UserAchievements.AnyAsync())
        {
            var seededAchievements = await db.Achievements.OrderBy(a => a.Id).Take(3).ToListAsync();
            if (seededAchievements.Count >= 3)
            {
                db.UserAchievements.AddRange(
                    new UserAchievement { UserId = "user-demo-1", AchievementId = seededAchievements[0].Id, UnlockedAt = DateTime.UtcNow.AddDays(-20) }, // First Blood
                    new UserAchievement { UserId = "user-demo-1", AchievementId = seededAchievements[2].Id, UnlockedAt = DateTime.UtcNow.AddDays(-5) }   // Streak Master
                );
                await db.SaveChangesAsync();
            }
        }
    }
}
