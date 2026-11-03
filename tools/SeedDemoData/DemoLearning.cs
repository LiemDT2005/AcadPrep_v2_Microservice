using System.Text.Json;
using Catalog.Domain.Entities;
using Catalog.Infrastructure.Persistence;
using Exam.Entities;
using Exam.Data;
using Identity.API.Models;
using Learning.Domain.Entities;
using Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Progress.Domain.Entities;
using Progress.Infrastructure.Persistence;

namespace SeedDemoData;

internal static class DemoLearning
{
    internal static async Task SeedAsync(CatalogDbContext catalog, LearningDbContext learning,
        ExamDbContext exam, ProgressDbContext progress, List<AppUser> users)
    {
        var courses = new[]
        {
            ("TOEIC Workplace Vocabulary", "Beginner", 500, "Vocabulary", "Office meetings, recruitment, invoices and business travel."),
            ("TOEIC Listening Booster 750+", "Intermediate", 750, "Listening", "Identify the speaker, purpose, location and next action in workplace conversations."),
            ("TOEIC Reading Mastery 850+", "Advanced", 850, "Reading", "Read emails, notices and multiple passages; find evidence for each answer.")
        };
        foreach (var (title, level, target, skill, description) in courses)
        {
            if (await catalog.Courses.AnyAsync(c => c.Title == title)) continue;
            var id = Guid.NewGuid();
            catalog.Courses.Add(new Course
            {
                Id = id, Title = title, Level = level, TargetScore = target, Description = description,
                Lessons = Enumerable.Range(1, 4).Select(i => new Lesson
                {
                    CourseId = id, Title = $"{skill}: Workshop {i}", Skill = skill, Order = i, DurationMinutes = 20 + 5 * i,
                    Content = $"{description}\nExample: Please submit the revised proposal by Friday.\nPractice {i}: identify the deadline, responsible person and requested action. Explain your answer using evidence from the sentence."
                }).ToList()
            });
        }
        await catalog.SaveChangesAsync();

        var words = new (string Word, string Meaning, string Example)[]
        {
            ("Invoice", "Hóa đơn / a request for payment", "Please send the invoice to the accounting department."),
            ("Deadline", "Hạn chót / the latest time to complete a task", "The deadline for applications is Friday."),
            ("Recruitment", "Tuyển dụng / the process of hiring employees", "Recruitment for the new branch begins in June."),
            ("Itinerary", "Lịch trình / a travel schedule", "Your itinerary includes a visit to the regional office."),
            ("Reimbursement", "Hoàn trả chi phí / repayment of an expense", "Keep your receipts to request reimbursement."),
            ("Agenda", "Chương trình họp / a list of meeting topics", "The updated agenda is attached to this email."),
            ("Warehouse", "Kho hàng / a building used to store goods", "The shipment will arrive at the warehouse tomorrow."),
            ("Reservation", "Đặt chỗ / an arrangement to hold a seat or room", "I would like to confirm my hotel reservation."),
            ("Maintenance", "Bảo trì / work that keeps equipment operating", "Scheduled maintenance will take place this weekend."),
            ("Proposal", "Đề xuất / a formal suggestion or plan", "The committee approved the revised proposal."),
            ("Eligible", "Đủ điều kiện / qualified to participate", "All full-time employees are eligible for the workshop."),
            ("Postpone", "Hoãn / delay until a later time", "We have postponed the meeting until next Monday."),
            ("Confirm", "Xác nhận / establish that an arrangement is definite", "Please confirm your attendance by Thursday."),
            ("Shipment", "Lô hàng / goods being transported", "The shipment contains fifty office chairs."),
            ("Warranty", "Bảo hành / a written guarantee for a product", "This printer comes with a two-year warranty.")
        };
        foreach (var row in words)
            if (!await learning.Vocabularies.AnyAsync(v => v.Word == row.Word))
                learning.Vocabularies.Add(new Vocabulary { Word = row.Word, Meaning = row.Meaning, Example = row.Example,
                    Passages = [new VocabPassage { Content = $"Office English practice: {row.Example} Contact the administration team if you need further assistance." }] });
        await learning.SaveChangesAsync();
        var vocabularies = await learning.Vocabularies.OrderBy(v => v.Id).ToListAsync();
        var achievements = await learning.Achievements.ToListAsync();
        var demoExam = await exam.Exams.Include(x => x.Questions).FirstAsync(x => x.Title == DemoContent.Title);
        var questions = demoExam.Questions.OrderBy(q => q.QuestionNumber).ToList();
        var now = DateTime.UtcNow;
        for (var userIndex = 0; userIndex < users.Count; userIndex++)
        {
            var user = users[userIndex];
            if (!await learning.UserProfiles.AnyAsync(x => x.UserId == user.Id))
                learning.UserProfiles.Add(new UserProfileSnapshot { UserId = user.Id, Email = user.Email!, FullName = user.FullName, CreatedAt = user.CreatedAt, IsActive = user.Status == UserStatus.Active });
            var streak = 2 + userIndex * 2;
            if (!await learning.StudyStreaks.AnyAsync(x => x.UserId == user.Id))
                learning.StudyStreaks.Add(new StudyStreak { UserId = user.Id, CurrentStreak = streak, MaxStreak = streak + 3, LastActiveDate = DateOnly.FromDateTime(now) });
            foreach (var (word, index) in vocabularies.Take(10 + userIndex).Select((v,i) => (v,i)))
                if (!await learning.SavedVocabularies.AnyAsync(x => x.UserId == user.Id && x.VocabularyId == word.Id))
                    learning.SavedVocabularies.Add(new SavedVocabulary { UserId = user.Id, VocabularyId = word.Id, DateSaved = now.AddDays(-index-1), Interval = index % 3 + 1,
                        NextReviewDate = index % 2 == 0 ? now.AddDays(-1) : now.AddDays(index % 3 + 1) });
            foreach (var achievement in achievements.Where(a => a.ConditionType == "ExamsCompleted" && a.ConditionValue <= 4 || a.ConditionType == "Streak" && a.ConditionValue <= streak))
                if (!await learning.UserAchievements.AnyAsync(x => x.UserId == user.Id && x.AchievementId == achievement.Id))
                    learning.UserAchievements.Add(new UserAchievement { UserId = user.Id, AchievementId = achievement.Id, UnlockedAt = now.AddDays(-2) });

            var attempts = await exam.ExamAttempts.Where(a => a.ExamId == demoExam.Id && a.UserId == user.Id && a.IsSubmitted)
                .OrderBy(a => a.StartedAt).ToListAsync();
            for (var slot = attempts.Count; slot < 4; slot++)
            {
                var completed = now.Date.AddDays(-27 + slot * 8).AddHours(9 + userIndex);
                var listeningCorrect = Math.Min(95, 48 + userIndex * 4 + slot * 7);
                var readingCorrect = Math.Min(95, 42 + userIndex * 4 + slot * 7);
                var a = new ExamAttempt { UserId = user.Id, ExamId = demoExam.Id, StartedAt = completed.AddMinutes(-110), CompletedAt = completed, IsSubmitted = true, RemainingTime = 600 };
                var l = 0; var r = 0;
                foreach (var q in questions)
                {
                    var correct = q.Part <= 4 ? l++ < listeningCorrect : r++ < readingCorrect;
                    a.AttemptAnswers.Add(new AttemptAnswer { QuestionId = q.Id, IsCorrect = correct,
                        SelectedOption = correct ? q.CorrectOption : (Exam.Entities.Enums.OptionLetter)(((int)q.CorrectOption + 1) % (q.Part == 2 ? 3 : 4)) });
                }
                a.ListeningScore = Learning.Application.Common.ToeicScoreConverter.CalculateListeningScore(listeningCorrect);
                a.ReadingScore = Learning.Application.Common.ToeicScoreConverter.CalculateReadingScore(readingCorrect);
                a.TotalScore = a.ListeningScore + a.ReadingScore;
                exam.ExamAttempts.Add(a);
                attempts.Add(a);
            }
            await exam.SaveChangesAsync();
            foreach (var a in attempts)
            {
                var id = new Guid(a.Id, 232, 1, new byte[8]);
                if (await learning.ExamAttemptRecords.AnyAsync(x => x.AttemptId == id)) continue;
                var answers = await exam.AttemptAnswers.Include(x => x.Question).Where(x => x.AttemptId == a.Id).ToListAsync();
                var mastery = answers.GroupBy(x => x.Question.Part).ToDictionary(g => g.Key.ToString(), g => Math.Round(100.0 * g.Count(x => x.IsCorrect) / g.Count(), 1));
                learning.ExamAttemptRecords.Add(new ExamAttemptRecord { AttemptId = id, UserId = user.Id, ExamId = new Guid(demoExam.Id, 232, 2, new byte[8]), ExamTitle = demoExam.Title,
                    Score = a.TotalScore, ListeningScore = a.ListeningScore, ReadingScore = a.ReadingScore,
                    TotalQuestions = answers.Count, CorrectAnswers = answers.Count(x => x.IsCorrect),
                    ListeningCorrect = answers.Count(x => x.Question.Part <= 4 && x.IsCorrect), ReadingCorrect = answers.Count(x => x.Question.Part >= 5 && x.IsCorrect),
                    PartMasteryJson = JsonSerializer.Serialize(mastery), SubmittedAt = a.CompletedAt!.Value });
            }
            await learning.SaveChangesAsync();
            // Include any previous sample records too, so Progress and Learning display the same history.
            foreach (var record in await learning.ExamAttemptRecords.Where(x => x.UserId == user.Id).ToListAsync())
                if (!await progress.LearningRecords.AnyAsync(x => x.AttemptId == record.AttemptId))
                    progress.LearningRecords.Add(new LearningRecord { AttemptId = record.AttemptId, UserId = user.Id, ExamId = record.ExamId,
                        ExamTitle = record.ExamTitle, Score = record.Score, CorrectAnswers = record.CorrectAnswers, TotalQuestions = record.TotalQuestions, CompletedAt = record.SubmittedAt });
            await progress.SaveChangesAsync();
        }
    }
}
