using Exam.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Exam.Infrastructure.Persistence;

public static class ExamSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();
        await db.Database.EnsureCreatedAsync();
        if (await db.PracticeExams.AnyAsync()) return;

        var examId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var q1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var q2 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
        var q3 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
        var q4 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4");
        var q5 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5");

        db.PracticeExams.Add(new PracticeExam
        {
            Id = examId,
            Title = "Mini Part 5 — Grammar Warm-up",
            Part = "Part5",
            Description = "5 câu Incomplete Sentences để demo nộp bài + event Progress.",
            TimeLimitMinutes = 5,
            Questions =
            [
                new Question { Id = q1, PracticeExamId = examId, Order = 1, CorrectAnswer = "B",
                    Stem = "The meeting has been _____ until next Monday.",
                    OptionA = "cancel", OptionB = "postponed", OptionC = "delaying", OptionD = "postpone" },
                new Question { Id = q2, PracticeExamId = examId, Order = 2, CorrectAnswer = "C",
                    Stem = "Ms. Park is responsible _____ training new staff.",
                    OptionA = "to", OptionB = "at", OptionC = "for", OptionD = "with" },
                new Question { Id = q3, PracticeExamId = examId, Order = 3, CorrectAnswer = "A",
                    Stem = "Please submit the report _____ Friday afternoon.",
                    OptionA = "by", OptionB = "until", OptionC = "during", OptionD = "since" },
                new Question { Id = q4, PracticeExamId = examId, Order = 4, CorrectAnswer = "D",
                    Stem = "The new software is _____ than the previous version.",
                    OptionA = "efficient", OptionB = "most efficient", OptionC = "efficiently", OptionD = "more efficient" },
                new Question { Id = q5, PracticeExamId = examId, Order = 5, CorrectAnswer = "B",
                    Stem = "All employees _____ wear their ID badges in the building.",
                    OptionA = "must to", OptionB = "must", OptionC = "musts", OptionD = "musting" }
            ]
        });

        await db.SaveChangesAsync();
    }
}
