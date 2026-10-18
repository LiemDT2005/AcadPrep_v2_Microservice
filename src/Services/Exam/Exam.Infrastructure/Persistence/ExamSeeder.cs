using Exam.Domain.Entities;
using Exam.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exam.Infrastructure.Persistence;

public static class ExamSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();

        // SQL Server might take 10-15 seconds to finish initial startup in Docker
        var retries = 10;
        while (retries > 0)
        {
            try
            {
                await db.Database.EnsureCreatedAsync();
                break;
            }
            catch (Exception)
            {
                retries--;
                if (retries == 0) throw;
                await Task.Delay(3000);
            }
        }


        // Upgrade the two legacy monolith user keys without dropping existing data.
        // EnsureCreated does not update schemas in databases that already exist.
        await db.Database.ExecuteSqlRawAsync("""
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'EXAM_ATTEMPTS') AND name = N'UserId' AND TYPE_NAME(user_type_id) = N'int')
                ALTER TABLE [EXAM_ATTEMPTS] ALTER COLUMN [UserId] nvarchar(450) NOT NULL;
            IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'PRACTICE_SESSIONS') AND name = N'UserId' AND TYPE_NAME(user_type_id) = N'int')
                ALTER TABLE [PRACTICE_SESSIONS] ALTER COLUMN [UserId] nvarchar(450) NOT NULL;
            """);

        // 1. Seed ExamSeries
        if (!await db.ExamSeries.AnyAsync())
        {
            var seriesList = new List<ExamSeries>
            {
                new() { Name = "ETS", Year = 2025, Description = "Official ETS TOEIC practice tests", CoverImageUrl = "https://images.unsplash.com/photo-1544716278-ca5e3f4abd8c?w=400", CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new() { Name = "ETS", Year = 2024, Description = "Official ETS TOEIC practice tests (2024 edition)", CoverImageUrl = "https://images.unsplash.com/photo-1497633762265-9d179a990aa6?w=400", CreatedAt = DateTime.UtcNow.AddDays(-120) },
                new() { Name = "New Economy", Year = 2025, Description = "Economy series – best-selling TOEIC prep in Vietnam", CoverImageUrl = "https://images.unsplash.com/photo-1457369804613-52c61a468e7d?w=400", CreatedAt = DateTime.UtcNow.AddDays(-55) },
                new() { Name = "New Economy", Year = 2024, Description = "Economy series – 2024 edition", CoverImageUrl = "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400", CreatedAt = DateTime.UtcNow.AddDays(-110) },
                new() { Name = "Hacker TOEIC", Year = 2025, Description = "Advanced difficulty TOEIC practice", CoverImageUrl = "https://images.unsplash.com/photo-1495446815901-a7297e633e8d?w=400", CreatedAt = DateTime.UtcNow.AddDays(-50) },
                new() { Name = "Luyện Đề", Year = 2025, Description = "Curated Vietnamese TOEIC practice collection", CoverImageUrl = "https://images.unsplash.com/photo-1434030216411-0b793f4b4173?w=400", CreatedAt = DateTime.UtcNow.AddDays(-45) }
            };

            db.ExamSeries.AddRange(seriesList);
            await db.SaveChangesAsync();
        }

        // 2. Seed Exams
        if (!await db.Exams.AnyAsync())
        {
            var allSeries = await db.ExamSeries.ToListAsync();
            var ets2025 = allSeries.First(s => s.Name == "ETS" && s.Year == 2025);
            var ets2024 = allSeries.First(s => s.Name == "ETS" && s.Year == 2024);
            var economy2025 = allSeries.First(s => s.Name == "New Economy" && s.Year == 2025);
            var economy2024 = allSeries.First(s => s.Name == "New Economy" && s.Year == 2024);
            var hacker2025 = allSeries.First(s => s.Name == "Hacker TOEIC" && s.Year == 2025);
            var luyenDe2025 = allSeries.First(s => s.Name == "Luyện Đề" && s.Year == 2025);

            var exams = new List<Domain.Entities.Exam>
            {
                // ETS 2025
                new() { Title = "ETS TOEIC 2025 Test 1", Duration = 120, Description = "Official Practice Test 1 – Full simulation", ExamSeriesId = ets2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-30) },
                new() { Title = "ETS TOEIC 2025 Test 2", Duration = 120, Description = "Official Practice Test 2 – Listening focus", ExamSeriesId = ets2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-28) },
                new() { Title = "ETS TOEIC 2025 Test 3", Duration = 120, Description = "Official Practice Test 3 – Reading focus", ExamSeriesId = ets2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-26) },
                new() { Title = "ETS TOEIC 2025 Test 4", Duration = 120, Description = "Official Practice Test 4 – Mixed difficulty", ExamSeriesId = ets2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-24) },

                // ETS 2024
                new() { Title = "ETS TOEIC 2024 Test 1", Duration = 120, Description = "Previous year official test – excellent baseline", ExamSeriesId = ets2024.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-90) },
                new() { Title = "ETS TOEIC 2024 Test 2", Duration = 120, Description = "Previous year official test – intermediate level", ExamSeriesId = ets2024.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-85) },
                new() { Title = "ETS TOEIC 2024 Test 3", Duration = 120, Description = "Previous year official test – advanced level", ExamSeriesId = ets2024.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-80) },

                // New Economy 2025
                new() { Title = "New Economy TOEIC Vol 7", Duration = 120, Description = "Latest Economy edition – trending practice set", ExamSeriesId = economy2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-22) },
                new() { Title = "New Economy TOEIC Vol 8", Duration = 120, Description = "Economy series – comprehensive review", ExamSeriesId = economy2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new() { Title = "New Economy TOEIC Vol 9", Duration = 120, Description = "Economy series – Part 5-6-7 heavy", ExamSeriesId = economy2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-18) },
                new() { Title = "New Economy TOEIC Vol 10", Duration = 120, Description = "Economy series – final mock exam", ExamSeriesId = economy2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-16) },

                // New Economy 2024
                new() { Title = "Economy TOEIC Vol 4", Duration = 120, Description = "2024 Economy edition – Part 3-4 intensive", ExamSeriesId = economy2024.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-75) },
                new() { Title = "Economy TOEIC Vol 5", Duration = 120, Description = "2024 Economy edition – balanced difficulty", ExamSeriesId = economy2024.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-70) },
                new() { Title = "Economy TOEIC Vol 6", Duration = 120, Description = "2024 Economy edition – exam simulation", ExamSeriesId = economy2024.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-65) },

                // Hacker TOEIC 2025
                new() { Title = "Hacker TOEIC Test 1", Duration = 120, Description = "Hard difficulty – designed for 800+ target scorers", ExamSeriesId = hacker2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-14) },
                new() { Title = "Hacker TOEIC Test 2", Duration = 120, Description = "Hard difficulty – tricky grammar & vocabulary", ExamSeriesId = hacker2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-12) },
                new() { Title = "Hacker TOEIC Test 3", Duration = 120, Description = "Hard difficulty – long passage reading comprehension", ExamSeriesId = hacker2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-10) },

                // Luyện Đề 2025
                new() { Title = "Luyện Đề TOEIC Đề 1", Duration = 120, Description = "Vietnamese curated practice – beginner friendly", ExamSeriesId = luyenDe2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new() { Title = "Luyện Đề TOEIC Đề 2", Duration = 120, Description = "Vietnamese curated practice – intermediate level", ExamSeriesId = luyenDe2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new() { Title = "Luyện Đề TOEIC Đề 3", Duration = 120, Description = "Vietnamese curated practice – full mock simulation", ExamSeriesId = luyenDe2025.Id, Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-4) }
            };

            db.Exams.AddRange(exams);
            await db.SaveChangesAsync();
        }

        // 3. Seed Questions & Options
        if (!await db.Questions.AnyAsync())
        {
            var exams = await db.Exams.ToListAsync();
            var partSpecs = new (int Part, int Count, string[] Tags)[]
            {
                (1, 6, ["Photographs", "Human Activities", "Objects"]),
                (2, 25, ["Question-Response", "Wh- Questions", "Yes/No Questions"]),
                (3, 39, ["Conversations", "Office Scenarios", "Customer Service"]),
                (4, 30, ["Short Talks", "Announcements", "Advertisements"]),
                (5, 30, ["Grammar", "Vocabulary", "Prepositions", "Pronouns"]),
                (6, 16, ["Text Completion", "Notices", "Letters", "Articles"]),
                (7, 54, ["Single Passages", "Double Passages", "Triple Passages", "Emails"])
            };

            foreach (var exam in exams.Take(5)) // Seed full structure for the top 5 exams
            {
                var qNum = 1;
                foreach (var spec in partSpecs)
                {
                    for (int i = 0; i < spec.Count; i++)
                    {
                        var tag = spec.Tags[i % spec.Tags.Length];
                        var question = new Question
                        {
                            ExamId = exam.Id,
                            Part = spec.Part,
                            QuestionNumber = qNum++,
                            QuestionText = $"Question {qNum - 1}: Part {spec.Part} sample question regarding {tag}.",
                            CorrectOption = (OptionLetter)(i % 4),
                            QuestionType = $"Part {spec.Part}",
                            TopicTag = tag,
                            QuestionOptions =
                            [
                                new() { OptionLetter = OptionLetter.A, OptionText = "Option A content for question" },
                                new() { OptionLetter = OptionLetter.B, OptionText = "Option B content for question" },
                                new() { OptionLetter = OptionLetter.C, OptionText = "Option C content for question" },
                                new() { OptionLetter = OptionLetter.D, OptionText = "Option D content for question" }
                            ]
                        };
                        db.Questions.Add(question);
                    }
                }
            }

            // For other exams, seed a representative set of questions
            foreach (var exam in exams.Skip(5))
            {
                var qNum = 1;
                foreach (var spec in partSpecs)
                {
                    var count = spec.Part switch { 1 => 2, 2 => 5, 3 => 6, 4 => 6, 5 => 8, 6 => 4, 7 => 10, _ => 5 };
                    for (int i = 0; i < count; i++)
                    {
                        var tag = spec.Tags[i % spec.Tags.Length];
                        var question = new Question
                        {
                            ExamId = exam.Id,
                            Part = spec.Part,
                            QuestionNumber = qNum++,
                            QuestionText = $"Question {qNum - 1}: Part {spec.Part} sample question regarding {tag}.",
                            CorrectOption = (OptionLetter)(i % 4),
                            QuestionType = $"Part {spec.Part}",
                            TopicTag = tag,
                            QuestionOptions =
                            [
                                new() { OptionLetter = OptionLetter.A, OptionText = "Option A content" },
                                new() { OptionLetter = OptionLetter.B, OptionText = "Option B content" },
                                new() { OptionLetter = OptionLetter.C, OptionText = "Option C content" },
                                new() { OptionLetter = OptionLetter.D, OptionText = "Option D content" }
                            ]
                        };
                        db.Questions.Add(question);
                    }
                }
            }

            await db.SaveChangesAsync();
        }

        // 4. Seed Attempts
        if (!await db.ExamAttempts.AnyAsync())
        {
            var exams = await db.Exams.Take(4).ToListAsync();
            var attempts = new List<ExamAttempt>
            {
                new() { UserId = "1", ExamId = exams[0].Id, ListeningScore = 380, ReadingScore = 350, TotalScore = 730, RemainingTime = 300, IsSubmitted = true, StartedAt = DateTime.UtcNow.AddDays(-10), CompletedAt = DateTime.UtcNow.AddDays(-10).AddMinutes(115) },
                new() { UserId = "1", ExamId = exams[0].Id, ListeningScore = 420, ReadingScore = 390, TotalScore = 810, RemainingTime = 540, IsSubmitted = true, StartedAt = DateTime.UtcNow.AddDays(-3), CompletedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(110) },
                new() { UserId = "1", ExamId = exams[1].Id, ListeningScore = 350, ReadingScore = 310, TotalScore = 660, RemainingTime = 120, IsSubmitted = true, StartedAt = DateTime.UtcNow.AddDays(-7), CompletedAt = DateTime.UtcNow.AddDays(-7).AddMinutes(118) },
                new() { UserId = "2", ExamId = exams[0].Id, ListeningScore = 400, ReadingScore = 420, TotalScore = 820, RemainingTime = 600, IsSubmitted = true, StartedAt = DateTime.UtcNow.AddDays(-5), CompletedAt = DateTime.UtcNow.AddDays(-5).AddMinutes(108) },
                new() { UserId = "3", ExamId = exams[2].Id, ListeningScore = 290, ReadingScore = 280, TotalScore = 570, RemainingTime = 0, IsSubmitted = true, StartedAt = DateTime.UtcNow.AddDays(-2), CompletedAt = DateTime.UtcNow.AddDays(-2).AddMinutes(120) }
            };

            db.ExamAttempts.AddRange(attempts);
            await db.SaveChangesAsync();
        }
    }
}
