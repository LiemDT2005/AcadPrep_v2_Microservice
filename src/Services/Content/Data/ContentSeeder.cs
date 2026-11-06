using Content.Entities;
using Content.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Content.Data;

public static class ContentSeeder
{
    public static async Task SeedAsync(ContentDbContext db, ILogger logger)
    {
        await db.Database.EnsureCreatedAsync();

        // Ensure Difficulty column exists on existing SQLite DB
        try
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Questions ADD COLUMN Difficulty TEXT DEFAULT 'Medium';");
        }
        catch
        {
            // Column already exists
        }

        // Ensure all ETS TOEIC series 2018 - 2026 are seeded
        foreach (var yr in new[] { 2018, 2019, 2020, 2021, 2022, 2023, 2024, 2025, 2026 })
        {
            if (!await db.ExamSeries.AnyAsync(s => s.Year == yr && s.Name.Contains("ETS")))
            {
                db.ExamSeries.Add(new ExamSeries
                {
                    Name = $"ETS TOEIC {yr}",
                    Year = yr,
                    Description = $"Official TOEIC tests published by ETS for year {yr}.",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                });
            }
        }
        await db.SaveChangesAsync();

        if (await db.Exams.AnyAsync())
        {
            return;
        }

        logger.LogInformation("==> Seeding Content Database with authentic TOEIC exams, questions, and passages across Parts 1-7...");

        // 1. Exam Series
        var seriesEts = new ExamSeries
        {
            Name = "ETS TOEIC 2024",
            Year = 2024,
            Description = "Official TOEIC tests published by ETS for year 2024.",
            CreatedAt = DateTime.UtcNow
        };

        var seriesNewEconomy = new ExamSeries
        {
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
            Title = "ETS TOEIC 2024 Test 1",
            ExamType = "Full Test",
            Description = "Standard 2-hour full test with listening audio and reading questions.",
            Duration = 120,
            AudioUrl = "/audio/sample-listening.wav",
            Status = ExamStatus.Published,
            IsDeleted = false,
            ExamSeriesId = seriesEts.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            LastModifiedAt = DateTime.UtcNow.AddDays(-2)
        };

        var exam2 = new Exam
        {
            Title = "ETS TOEIC 2024 Test 2",
            ExamType = "Full Test",
            Description = "Complete 200-question draft exam currently in content review.",
            Duration = 120,
            AudioUrl = "/audio/sample-listening.wav",
            Status = ExamStatus.Draft,
            IsDeleted = false,
            ExamSeriesId = seriesEts.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };

        var exam3 = new Exam
        {
            Title = "New Economy TOEIC Test 1",
            ExamType = "Full Test",
            Description = "Archived exam with preserved learner attempts.",
            Duration = 120,
            Status = ExamStatus.Hidden,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-1),
            ExamSeriesId = seriesNewEconomy.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var exam4 = new Exam
        {
            Title = "TOEIC Reading Mini Test 01",
            ExamType = "Reading",
            Description = "Concentrated 45-minute reading test covering Parts 5, 6, and 7.",
            Duration = 45,
            Status = ExamStatus.Published,
            IsDeleted = false,
            ExamSeriesId = seriesEts.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };

        var exam5 = new Exam
        {
            Title = "TOEIC Listening Part 1-2 Practice",
            ExamType = "Listening",
            Description = "Intensive drills for photographs and question-response skills.",
            Duration = 30,
            AudioUrl = "/audio/sample-listening.wav",
            Status = ExamStatus.Published,
            IsDeleted = false,
            ExamSeriesId = seriesNewEconomy.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-4)
        };

        db.Exams.AddRange(exam1, exam2, exam3, exam4, exam5);
        await db.SaveChangesAsync();

        // 3. Questions & Structure for Exam 1 & Exam 2
        var p1Questions = new List<Question>
        {
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 1, Part = 1,
                QuestionText = "Look at the picture. Select the statement that best describes what you see in the photograph.",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 10, AudioEndSecond = 20,
                CorrectOption = "C", Difficulty = "Easy", QuestionType = "Photograph", TopicTag = "Office",
                Explanation = "The man is working at the laptop desk.",
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
                ExamId = exam1.Id, QuestionNumber = 2, Part = 1,
                QuestionText = "Look at the picture. Select the statement that best describes the scene.",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 25, AudioEndSecond = 35,
                CorrectOption = "A", Difficulty = "Easy", QuestionType = "Photograph", TopicTag = "Travel",
                Explanation = "Passengers are waiting on the station platform.",
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
                ExamId = exam1.Id, QuestionNumber = 7, Part = 2,
                QuestionText = "Where did you leave the contract files?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 40, AudioEndSecond = 48,
                CorrectOption = "B", Difficulty = "Easy", QuestionType = "Where Question", TopicTag = "Office",
                Explanation = "States location on Ms. Nguyen's desk.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Yes, I signed them yesterday." },
                    new() { OptionLetter = "B", OptionText = "On Ms. Nguyen's desk in room 302." },
                    new() { OptionLetter = "C", OptionText = "About two hours ago." }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 8, Part = 2,
                QuestionText = "When will the quarterly finance presentation begin?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 50, AudioEndSecond = 58,
                CorrectOption = "A", Difficulty = "Medium", QuestionType = "When Question", TopicTag = "Finance",
                Explanation = "Direct answer indicating start time at 2 PM.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Right after the lunch break at 2:00 PM." },
                    new() { OptionLetter = "B", OptionText = "In the third-floor auditorium." },
                    new() { OptionLetter = "C", OptionText = "Dr. Alvarez will present." }
                }
            }
        };

        // Part 3 & 4 Listening Groups
        var p3Group = new QuestionGroup
        {
            ExamId = exam1.Id,
            Name = "Part 3 - Questions 32-34: Project Budget Review",
            AudioUrl = "/audio/sample-listening.wav",
            AudioStartSecond = 60,
            AudioEndSecond = 95,
            Explanation = "A conversation between two project managers discussing Q4 marketing expenses."
        };
        var p4Group = new QuestionGroup
        {
            ExamId = exam1.Id,
            Name = "Part 4 - Questions 71-73: Airport Gate Announcement",
            AudioUrl = "/audio/sample-listening.wav",
            AudioStartSecond = 100,
            AudioEndSecond = 135,
            Explanation = "A gate agent announcing a boarding change for Flight 408 to Tokyo."
        };
        db.QuestionGroups.AddRange(p3Group, p4Group);
        await db.SaveChangesAsync();

        var p3p4Questions = new List<Question>
        {
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 32, Part = 3, QuestionGroupId = p3Group.Id,
                QuestionText = "What are the speakers mainly discussing?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 60, AudioEndSecond = 95,
                CorrectOption = "B", Difficulty = "Medium", QuestionType = "Main Idea", TopicTag = "Business",
                Explanation = "They are reviewing the marketing budget allocation.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Hiring new sales representatives" },
                    new() { OptionLetter = "B", OptionText = "Revising the fourth-quarter marketing budget" },
                    new() { OptionLetter = "C", OptionText = "Relocating the regional branch office" },
                    new() { OptionLetter = "D", OptionText = "Upgrading warehouse inventory software" }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 33, Part = 3, QuestionGroupId = p3Group.Id,
                QuestionText = "What does the woman suggest doing before Friday?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 60, AudioEndSecond = 95,
                CorrectOption = "C", Difficulty = "Medium", QuestionType = "Detail", TopicTag = "Business",
                Explanation = "She recommends getting updated quotes from the printing vendor.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Canceling the trade show booth" },
                    new() { OptionLetter = "B", OptionText = "Postponing the board meeting" },
                    new() { OptionLetter = "C", OptionText = "Requesting updated quotes from the vendor" },
                    new() { OptionLetter = "D", OptionText = "Sending a memo to all department staff" }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 34, Part = 3, QuestionGroupId = p3Group.Id,
                QuestionText = "What will the man most likely do next?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 60, AudioEndSecond = 95,
                CorrectOption = "A", Difficulty = "Hard", QuestionType = "Inference", TopicTag = "Business",
                Explanation = "He agrees to email the finance director right away.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Email the finance director" },
                    new() { OptionLetter = "B", OptionText = "Book a flight to Chicago" },
                    new() { OptionLetter = "C", OptionText = "Print copies of the brochure" },
                    new() { OptionLetter = "D", OptionText = "Call the client service desk" }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 71, Part = 4, QuestionGroupId = p4Group.Id,
                QuestionText = "Where is this announcement most likely being made?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 100, AudioEndSecond = 135,
                CorrectOption = "B", Difficulty = "Easy", QuestionType = "Location", TopicTag = "Travel",
                Explanation = "Gate numbers and boarding passes indicate an airport terminal.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "At a convention center" },
                    new() { OptionLetter = "B", OptionText = "At an airport terminal" },
                    new() { OptionLetter = "C", OptionText = "Inside a hotel lobby" },
                    new() { OptionLetter = "D", OptionText = "On a commuter train" }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 72, Part = 4, QuestionGroupId = p4Group.Id,
                QuestionText = "What has changed about the flight?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 100, AudioEndSecond = 135,
                CorrectOption = "D", Difficulty = "Medium", QuestionType = "Detail", TopicTag = "Travel",
                Explanation = "The departure gate was moved from B12 to C04.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "The arrival destination" },
                    new() { OptionLetter = "B", OptionText = "The flight crew" },
                    new() { OptionLetter = "C", OptionText = "The baggage allowance" },
                    new() { OptionLetter = "D", OptionText = "The departure gate" }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 73, Part = 4, QuestionGroupId = p4Group.Id,
                QuestionText = "What are passengers asked to have ready?",
                AudioUrl = "/audio/sample-listening.wav", AudioStartSecond = 100, AudioEndSecond = 135,
                CorrectOption = "A", Difficulty = "Easy", QuestionType = "Detail", TopicTag = "Travel",
                Explanation = "Passengers must show their passport and boarding pass.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Their passport and boarding pass" },
                    new() { OptionLetter = "B", OptionText = "Their customs declaration form" },
                    new() { OptionLetter = "C", OptionText = "Their hotel reservation voucher" },
                    new() { OptionLetter = "D", OptionText = "Their frequent flyer card" }
                }
            }
        };

        var p5Questions = new List<Question>
        {
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 101, Part = 5,
                QuestionText = "The quarterly marketing summary must be submitted to the committee ______ 5:00 PM on Friday.",
                CorrectOption = "B", Difficulty = "Easy", QuestionType = "Preposition", TopicTag = "Marketing",
                Explanation = "Use 'before' + point in time to indicate a deadline.",
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
                ExamId = exam1.Id, QuestionNumber = 102, Part = 5,
                QuestionText = "All employees are ______ encouraged to enroll in the digital security certification workshop.",
                CorrectOption = "C", Difficulty = "Medium", QuestionType = "Word Form", TopicTag = "HR",
                Explanation = "'strongly encouraged' is a standard adverb + past participle collocation.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "strong" },
                    new() { OptionLetter = "B", OptionText = "strength" },
                    new() { OptionLetter = "C", OptionText = "strongly" },
                    new() { OptionLetter = "D", OptionText = "strengthen" }
                }
            },
            new()
            {
                ExamId = exam2.Id, QuestionNumber = 103, Part = 5,
                QuestionText = "______ the inclement weather, the annual charity marathon proceeded as scheduled.",
                CorrectOption = "A", Difficulty = "Hard", QuestionType = "Conjunction", TopicTag = "Events",
                Explanation = "'Despite' is followed by a noun phrase ('the inclement weather').",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "Despite" },
                    new() { OptionLetter = "B", OptionText = "Although" },
                    new() { OptionLetter = "C", OptionText = "Even though" },
                    new() { OptionLetter = "D", OptionText = "Whereas" }
                }
            }
        };

        // Part 6 Passage & 4 Questions
        var p6Passage = new Passage
        {
            ExamId = exam1.Id,
            DisplayOrder = 1,
            Content = "To: All Staff\nFrom: Human Resources\nSubject: Annual Health & Wellness Fair\n\nWe are pleased to announce that our Annual Health & Wellness Fair will take place on Wednesday, November 12, in Conference Hall B. Certified nutritionists and fitness instructors will be (131) ______ to provide personalized consultations. Employees who wish to register for free health screenings must complete the online form (132) ______ November 5. (133) ______. Please contact the HR desk if you have any dietary (134) ______ for the complimentary lunch.",
            Explanation = "Internal company email announcing an employee wellness event."
        };
        db.Passages.Add(p6Passage);
        await db.SaveChangesAsync();

        var p6Questions = new List<Question>
        {
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 131, Part = 6, PassageId = p6Passage.Id,
                QuestionText = "Blank (131)",
                CorrectOption = "B", Difficulty = "Easy", QuestionType = "Vocabulary", TopicTag = "HR",
                Explanation = "'available' fits the context of specialists being present to help.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "vacant" },
                    new() { OptionLetter = "B", OptionText = "available" },
                    new() { OptionLetter = "C", OptionText = "applicable" },
                    new() { OptionLetter = "D", OptionText = "probable" }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 132, Part = 6, PassageId = p6Passage.Id,
                QuestionText = "Blank (132)",
                CorrectOption = "A", Difficulty = "Easy", QuestionType = "Preposition", TopicTag = "HR",
                Explanation = "'by' indicates a registration deadline.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "by" },
                    new() { OptionLetter = "B", OptionText = "until" },
                    new() { OptionLetter = "C", OptionText = "since" },
                    new() { OptionLetter = "D", OptionText = "among" }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 133, Part = 6, PassageId = p6Passage.Id,
                QuestionText = "Blank (133)",
                CorrectOption = "C", Difficulty = "Hard", QuestionType = "Sentence Insertion", TopicTag = "HR",
                Explanation = "Connects registration requirements with limited appointment slots.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "The building was renovated two years ago." },
                    new() { OptionLetter = "B", OptionText = "Several software engineers attended the seminar." },
                    new() { OptionLetter = "C", OptionText = "Space is limited, so early sign-up is highly recommended." },
                    new() { OptionLetter = "D", OptionText = "Our accounting team will process travel reimbursements." }
                }
            },
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 134, Part = 6, PassageId = p6Passage.Id,
                QuestionText = "Blank (134)",
                CorrectOption = "D", Difficulty = "Medium", QuestionType = "Vocabulary", TopicTag = "HR",
                Explanation = "'dietary restrictions' is a standard phrase.",
                QuestionOptions = new List<QuestionOption>
                {
                    new() { OptionLetter = "A", OptionText = "permissions" },
                    new() { OptionLetter = "B", OptionText = "subscriptions" },
                    new() { OptionLetter = "C", OptionText = "Foundations" },
                    new() { OptionLetter = "D", OptionText = "restrictions" }
                }
            }
        };

        // Part 7 QuestionGroup + Passage + Questions
        var p7Group = new QuestionGroup
        {
            ExamId = exam1.Id,
            Name = "Questions 147-148 refer to the following memorandum.",
            Explanation = "Single passage reading comprehension set."
        };
        db.QuestionGroups.Add(p7Group);
        await db.SaveChangesAsync();

        var p7Passage = new Passage
        {
            ExamId = exam1.Id,
            QuestionGroupId = p7Group.Id,
            DisplayOrder = 1,
            Content = "MEMORANDUM\nTo: All Department Heads\nFrom: Facilities Management\nDate: October 14\nSubject: Scheduled Network Infrastructure Upgrade\n\nPlease be informed that our central servers will undergo essential maintenance this coming Saturday from 10:00 PM until 4:00 AM Sunday. Internet access and cloud services will be temporarily suspended during this window.",
            Explanation = "Announcement regarding server maintenance schedule."
        };

        // Draft standalone passage in Exam 2 (0 questions attached, ready for testing UC-18.4 & UC-18.5)
        var draftPassage = new Passage
        {
            ExamId = exam2.Id,
            DisplayOrder = 1,
            Content = "NOTICE TO VISITORS\nWelcome to Apex Innovation Labs. All visitors are required to sign in at the main security desk and wear a temporary visitor badge at all times while on the premises.",
            Explanation = "Sample standalone reading passage in Draft Exam 2 (0 attached questions)."
        };

        db.Passages.AddRange(p7Passage, draftPassage);
        await db.SaveChangesAsync();

        var p7Questions = new List<Question>
        {
            new()
            {
                ExamId = exam1.Id, QuestionNumber = 147, Part = 7, PassageId = p7Passage.Id, QuestionGroupId = p7Group.Id,
                QuestionText = "What is the primary purpose of the memorandum?",
                CorrectOption = "C", Difficulty = "Medium", QuestionType = "Main Purpose", TopicTag = "IT & Technology",
                Explanation = "To notify staff about scheduled network maintenance.",
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
                ExamId = exam1.Id, QuestionNumber = 148, Part = 7, PassageId = p7Passage.Id, QuestionGroupId = p7Group.Id,
                QuestionText = "According to the memo, when will server operations resume normally?",
                CorrectOption = "D", Difficulty = "Easy", QuestionType = "Detail", TopicTag = "IT & Technology",
                Explanation = "Maintenance finishes at 4:00 AM Sunday.",
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
        db.Questions.AddRange(p3p4Questions);
        db.Questions.AddRange(p5Questions);
        db.Questions.AddRange(p6Questions);
        db.Questions.AddRange(p7Questions);
        await db.SaveChangesAsync();

        logger.LogInformation("==> Content Database seeded successfully!");
    }
}
