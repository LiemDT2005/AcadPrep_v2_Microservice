using Content.Domain.Entities;
using Content.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Content.Infrastructure.Data;

public static class ContentSeeder
{
    public static async Task SeedAsync(ContentDbContext db, ILogger logger)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.ExamSeries.AnyAsync())
        {
            return;
        }

        logger.LogInformation("==> Seeding Content Database with authentic TOEIC exams and questions...");

        // 1. Exam Series
        var seriesEts = new ExamSeries
        {
            Id = 1,
            Name = "ETS TOEIC 2024",
            Year = 2024,
            Description = "Official TOEIC tests published by ETS for year 2024.",
            CreatedAt = DateTime.UtcNow
        };

        var seriesNewEconomy = new ExamSeries
        {
            Id = 2,
            Name = "New Economy TOEIC 2023",
            Year = 2023,
            Description = "High-accuracy full-length TOEIC preparation tests.",
            CreatedAt = DateTime.UtcNow
        };

        db.ExamSeries.AddRange(seriesEts, seriesNewEconomy);
        await db.SaveChangesAsync();

        // 2. Exams
        var exam1 = new Exam
        {
            Id = 1,
            Title = "ETS TOEIC 2024 Test 1",
            ExamType = "Full Test",
            Description = "Standard 2-hour full test with listening audio and reading questions.",
            Duration = 120,
            AudioUrl = "/audio/sample-listening.wav",
            Status = ExamStatus.Published,
            IsDeleted = false,
            ExamSeriesId = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            LastModifiedAt = DateTime.UtcNow.AddDays(-2)
        };

        var exam2 = new Exam
        {
            Id = 2,
            Title = "ETS TOEIC 2024 Test 2",
            ExamType = "Full Test",
            Description = "Complete 200-question draft exam currently in content review.",
            Duration = 120,
            AudioUrl = "/audio/sample-listening.wav",
            Status = ExamStatus.Draft,
            IsDeleted = false,
            ExamSeriesId = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };

        var exam3 = new Exam
        {
            Id = 3,
            Title = "New Economy TOEIC Test 1",
            ExamType = "Full Test",
            Description = "Archived exam with preserved learner attempts.",
            Duration = 120,
            Status = ExamStatus.Hidden,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-1),
            ExamSeriesId = 2,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var exam4 = new Exam
        {
            Id = 4,
            Title = "TOEIC Reading Mini Test 01",
            ExamType = "Reading",
            Description = "Concentrated 45-minute reading test covering Parts 5, 6, and 7.",
            Duration = 45,
            Status = ExamStatus.Published,
            IsDeleted = false,
            ExamSeriesId = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };

        var exam5 = new Exam
        {
            Id = 5,
            Title = "TOEIC Listening Part 1-2 Practice",
            ExamType = "Listening",
            Description = "Intensive drills for photographs and question-response skills.",
            Duration = 30,
            AudioUrl = "/audio/sample-listening.wav",
            Status = ExamStatus.Published,
            IsDeleted = false,
            ExamSeriesId = 2,
            CreatedAt = DateTime.UtcNow.AddDays(-4)
        };

        db.Exams.AddRange(exam1, exam2, exam3, exam4, exam5);
        await db.SaveChangesAsync();

        // 3. Questions & Structure for Exam 1
        var p1Questions = new List<Question>
        {
            new()
            {
                ExamId = 1, QuestionNumber = 1, Part = 1,
                QuestionText = "Look at the picture. Select the statement that best describes what you see in the photograph.",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 10, AudioEndSecond = 20,
                CorrectOption = "C", Explanation = "The man is working at the laptop desk.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "He is walking along the corridor." },
                    new() { OptionLetter = "B", OptionText = "He is painting the office wall." },
                    new() { OptionLetter = "C", OptionText = "He is typing on a notebook computer." },
                    new() { OptionLetter = "D", OptionText = "He is turning off the conference room light." }
                }
            },
            new()
            {
                ExamId = 1, QuestionNumber = 2, Part = 1,
                QuestionText = "Look at the picture. Select the statement that best describes the scene.",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 25, AudioEndSecond = 35,
                CorrectOption = "A", Explanation = "Passengers are waiting on the station platform.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "People are standing on the train platform." },
                    new() { OptionLetter = "B", OptionText = "A train is arriving at the gate." },
                    new() { OptionLetter = "C", OptionText = "Luggage is being unloaded from the cargo area." },
                    new() { OptionLetter = "D", OptionText = "Tickets are being stamped at the booth." }
                }
            }
        };

        var p2Questions = new List<Question>
        {
            new()
            {
                ExamId = 1, QuestionNumber = 7, Part = 2,
                QuestionText = "Where did you leave the contract files?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 40, AudioEndSecond = 48,
                CorrectOption = "B", Explanation = "States location on Ms. Nguyen's desk.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Yes, I signed them yesterday." },
                    new() { OptionLetter = "B", OptionText = "On Ms. Nguyen's desk in room 302." },
                    new() { OptionLetter = "C", OptionText = "About two hours ago." }
                }
            },
            new()
            {
                ExamId = 1, QuestionNumber = 8, Part = 2,
                QuestionText = "When will the quarterly finance presentation begin?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 50, AudioEndSecond = 58,
                CorrectOption = "A", Explanation = "Direct answer indicating start time at 2 PM.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Right after the lunch break at 2:00 PM." },
                    new() { OptionLetter = "B", OptionText = "In the third-floor auditorium." },
                    new() { OptionLetter = "C", OptionText = "Dr. Alvarez will present." }
                }
            }
        };

        var p5Questions = new List<Question>
        {
            new()
            {
                ExamId = 1, QuestionNumber = 101, Part = 5,
                QuestionText = "The quarterly marketing summary must be submitted to the committee ______ 5:00 PM on Friday.",
                CorrectOption = "B", QuestionType = "Preposition", Explanation = "Use 'before' + time.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "during" },
                    new() { OptionLetter = "B", OptionText = "before" },
                    new() { OptionLetter = "C", OptionText = "between" },
                    new() { OptionLetter = "D", OptionText = "inside" }
                }
            },
            new()
            {
                ExamId = 1, QuestionNumber = 102, Part = 5,
                QuestionText = "All employees are ______ encouraged to enroll in the digital security certification workshop.",
                CorrectOption = "C", QuestionType = "Adverb", Explanation = "'strongly encouraged' is a common collocation.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "strong" },
                    new() { OptionLetter = "B", OptionText = "strength" },
                    new() { OptionLetter = "C", OptionText = "strongly" },
                    new() { OptionLetter = "D", OptionText = "strengthen" }
                }
            }
        };

        var p7Passage = new Passage
        {
            ExamId = 1,
            DisplayOrder = 1,
            Content = "MEMORANDUM\nTo: All Department Heads\nFrom: Facilities Management\nDate: October 14\nSubject: Scheduled Network Infrastructure Upgrade\n\nPlease be informed that our central servers will undergo essential maintenance this coming Saturday from 10:00 PM until 4:00 AM Sunday. Internet access and cloud services will be temporarily suspended during this window.",
            Explanation = "Announcement regarding server maintenance schedule."
        };
        db.Passages.Add(p7Passage);
        await db.SaveChangesAsync();

        var p7Questions = new List<Question>
        {
            new()
            {
                ExamId = 1, QuestionNumber = 147, Part = 7, PassageId = p7Passage.Id,
                QuestionText = "What is the primary purpose of the memorandum?",
                CorrectOption = "C", Explanation = "To notify staff about scheduled network maintenance.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "To announce a new manager hire" },
                    new() { OptionLetter = "B", OptionText = "To distribute revised holiday schedules" },
                    new() { OptionLetter = "C", OptionText = "To announce planned system maintenance" },
                    new() { OptionLetter = "D", OptionText = "To review cloud storage subscription costs" }
                }
            },
            new()
            {
                ExamId = 1, QuestionNumber = 148, Part = 7, PassageId = p7Passage.Id,
                QuestionText = "According to the memo, when will server operations resume normally?",
                CorrectOption = "D", Explanation = "Maintenance finishes at 4:00 AM Sunday.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Friday afternoon" },
                    new() { OptionLetter = "B", OptionText = "Saturday at 10:00 PM" },
                    new() { OptionLetter = "C", OptionText = "Midnight Saturday" },
                    new() { OptionLetter = "D", OptionText = "Sunday at 4:00 AM" }
                }
            }
        };

        db.Questions.AddRange(p1Questions);
        db.Questions.AddRange(p2Questions);
        db.Questions.AddRange(p5Questions);
        db.Questions.AddRange(p7Questions);
        await db.SaveChangesAsync();

        logger.LogInformation("==> Content Database seeded successfully!");
    }
}
