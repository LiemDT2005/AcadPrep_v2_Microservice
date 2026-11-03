using Content.Data;
using Exam.Data;
using Microsoft.EntityFrameworkCore;
using C = Content.Entities;
using E = Exam.Entities;

namespace SeedDemoData;

internal static class DemoContent
{
    internal const string Title = "AcadPrep Demo — Office English Full Test";
    internal static async Task SeedAsync(ContentDbContext content, ExamDbContext exam)
    {
        // The two stores currently own separate exam models. Add an equivalent demo to each without replacing existing exams.
        var c = await content.Exams.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Title == Title);
        if (c is null)
        {
            c = new C.Exam { Title = Title, Description = "Synthetic demo exam for UI testing; repeated practice patterns, not an official TOEIC test. Listening uses the bundled sample clip.", Duration = 120,
                ExamType = "Full Test", Status = Content.Entities.Enums.ExamStatus.Published, AudioUrl = "/audio/sample-listening.wav",
                ExamSeriesId = await content.ExamSeries.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync() };
            content.Exams.Add(c);
            await content.SaveChangesAsync();
        }
        var e = await exam.Exams.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Title == Title);
        if (e is null)
        {
            e = new E.Exam { Title = Title, Description = c.Description, Duration = 120, Status = Exam.Entities.Enums.ExamStatus.Published,
                AudioUrl = c.AudioUrl, ExamSeriesId = await exam.ExamSeries.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync() };
            exam.Exams.Add(e);
            await exam.SaveChangesAsync();
        }
        // Each store commits its whole question graph in one transaction; a rerun adds only missing question numbers.
        var cNumbers = c.Questions.Select(q => q.QuestionNumber).ToHashSet();
        var eNumbers = e.Questions.Select(q => q.QuestionNumber).ToHashSet();
        var number = 0;
        foreach (var (part, count) in new[] { (1,6), (2,25), (3,39), (4,30), (5,30), (6,16), (7,54) })
        {
            C.QuestionGroup? cg = null; E.QuestionGroup? eg = null;
            C.Passage? cp = null; E.Passage? ep = null;
            for (var i = 0; i < count; i++)
            {
                number++;
                var groupSize = part == 6 ? 4 : 3;
                if (part is 3 or 4 or 6 or 7 && i % groupSize == 0)
                {
                    var passage = part is 6 or 7
                        ? "OFFICE NOTICE\nThe employee training workshop will take place on Friday at 2 p.m. in Room 302. Please register with Ms. Nguyen by Thursday. Participants should bring a laptop. The company will provide all training materials."
                        : "Demo listening transcript: The employee training workshop begins at 2 p.m. on Friday in Room 302. Ms. Nguyen is collecting registration forms. Participants need to bring a laptop.";
                    cg = new C.QuestionGroup { Name = $"Demo Part {part} Set {i / groupSize + 1}", ExamId = c.Id, AudioUrl = part < 5 ? c.AudioUrl : null };
                    eg = new E.QuestionGroup { Name = cg.Name, ExamId = e.Id, AudioUrl = cg.AudioUrl };
                    cp = new C.Passage { ExamId = c.Id, Content = passage, DisplayOrder = i / groupSize + 1, QuestionGroup = cg };
                    ep = new E.Passage { ExamId = e.Id, Content = passage, DisplayOrder = cp.DisplayOrder, QuestionGroup = eg };
                }
                var (text, options, correct, explanation) = Question(part, i);
                if (!cNumbers.Contains(number))
                    content.Questions.Add(new C.Question
                    {
                        ExamId = c.Id, QuestionNumber = number, Part = part, QuestionText = text,
                        QuestionType = $"Part {part}", TopicTag = part < 5 ? "Office Listening" : "Office English",
                        CorrectOption = correct, Explanation = explanation, AudioUrl = part < 5 ? c.AudioUrl : null,
                        Passage = cp, QuestionGroup = cg,
                        QuestionOptions = options.Select((o,j) => new C.QuestionOption { OptionLetter = ((char)('A'+j)).ToString(), OptionText = o }).ToList()
                    });
                if (!eNumbers.Contains(number))
                    exam.Questions.Add(new E.Question
                    {
                        ExamId = e.Id, QuestionNumber = number, Part = part, QuestionText = text,
                        QuestionType = $"Part {part}", TopicTag = part < 5 ? "Office Listening" : "Office English",
                        CorrectOption = Enum.Parse<Exam.Entities.Enums.OptionLetter>(correct), Explanation = explanation,
                        AudioUrl = part < 5 ? e.AudioUrl : null, Passage = ep, QuestionGroup = eg,
                        QuestionOptions = options.Select((o,j) => new E.QuestionOption { OptionLetter = (Exam.Entities.Enums.OptionLetter)j, OptionText = o }).ToList()
                    });
            }
        }
        await content.SaveChangesAsync();
        await exam.SaveChangesAsync();
    }

    private static (string Text, string[] Options, string Correct, string Explanation) Question(int part, int index)
    {
        if (part == 5)
            return (index % 3) switch
            {
                0 => ("Please submit the completed application _____ Friday.", ["by", "among", "during", "across"], "A", "Use by to state a deadline."),
                1 => ("The manager _____ the new training schedule yesterday.", ["announce", "announcing", "announced", "announcement"], "C", "Yesterday requires the past tense announced."),
                _ => ("All visitors must wear their identification badges _____.", ["visible", "visibly", "visibility", "vision"], "B", "The adverb visibly modifies wear.")
            };
        if (part == 2) return ("When does the workshop begin? (Demo prompt)", ["In Room 302.", "At two o'clock on Friday.", "Ms. Nguyen will register."], "B", "The question asks for a time.");
        if (part == 1) return ("Demo scene: an employee is typing on a laptop at an office desk. Which sentence describes the scene?", ["A man is painting a wall.", "An employee is using a computer.", "Someone is carrying luggage.", "A woman is boarding a train."], "B", "The demo scene describes an employee using a laptop.");
        return (index % 3) switch
        {
            0 => ("When will the employee training workshop take place?", ["Thursday at noon", "Friday at 2 p.m.", "Monday morning", "Saturday evening"], "B", "The notice/transcript gives Friday at 2 p.m."),
            1 => ("Who is responsible for workshop registration?", ["The facilities manager", "The receptionist", "Ms. Nguyen", "The IT director"], "C", "Participants register with Ms. Nguyen."),
            _ => ("What should participants bring?", ["A laptop", "Printed materials", "Lunch vouchers", "A visitor badge"], "A", "The notice/transcript asks participants to bring a laptop.")
        };
    }
}
