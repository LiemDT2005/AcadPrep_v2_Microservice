using Storefront.Web.Models;

namespace Storefront.Web.Pages.Admin.Exams;

public class ReadingSetViewModel
{
    public int PassageId { get; set; }
    public string? PassageContent { get; set; }
    public string? ImageUrl { get; set; }
    public List<AdminQuestionDetail> Questions { get; set; } = [];
}

public class ExamStructureViewModel
{
    public required AdminExamDetail Exam { get; init; }
    public bool IsReadOnly { get; init; }

    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }

    public const int TargetP1 = 6;
    public const int TargetP2 = 25;
    public const int TargetP3 = 39;
    public const int TargetP4 = 30;
    public const int TargetP5 = 30;
    public const int TargetP6 = 16;
    public const int TargetP7 = 54;

    public List<AdminQuestionDetail> Part1Questions { get; private set; } = [];
    public List<AdminQuestionDetail> Part2Questions { get; private set; } = [];
    public List<IGrouping<int?, AdminQuestionDetail>> Part3Groups { get; private set; } = [];
    public List<IGrouping<int?, AdminQuestionDetail>> Part4Groups { get; private set; } = [];
    public List<AdminQuestionDetail> Part5Questions { get; private set; } = [];
    public List<IGrouping<int?, AdminQuestionDetail>> Part6Groups { get; private set; } = [];
    public List<ReadingSetViewModel> Part7Sets { get; private set; } = [];
    public List<AdminQuestionDetail> Part7Orphans { get; private set; } = [];

    public int Part1Count { get; private set; }
    public int Part2Count { get; private set; }
    public int Part3Count { get; private set; }
    public int Part4Count { get; private set; }
    public int Part5Count { get; private set; }
    public int Part6Count { get; private set; }
    public int Part7Count { get; private set; }
    public int TotalQuestions { get; private set; }

    public bool CanAddP1 => Part1Count < TargetP1;
    public bool CanAddP2 => Part2Count < TargetP2;
    public bool CanAddP3 => Part3Count + 3 <= TargetP3;
    public bool CanAddP4 => Part4Count + 3 <= TargetP4;
    public bool CanAddP5 => Part5Count < TargetP5;
    public bool CanAddP6 => Part6Count + 4 <= TargetP6;
    public bool CanAddP7 => Part7Count + 2 <= TargetP7;

    public static ExamStructureViewModel From(
        AdminExamDetail exam,
        bool isReadOnly,
        string? successMessage = null,
        string? errorMessage = null)
    {
        var vm = new ExamStructureViewModel
        {
            Exam = exam,
            IsReadOnly = isReadOnly,
            SuccessMessage = successMessage,
            ErrorMessage = errorMessage
        };
        vm.Build();
        return vm;
    }

    private void Build()
    {
        var qs = Exam.Questions ?? [];
        Part1Questions = qs.Where(q => q.Part == 1).OrderBy(q => q.QuestionNumber).ToList();
        Part2Questions = qs.Where(q => q.Part == 2).OrderBy(q => q.QuestionNumber).ToList();
        Part3Groups = qs.Where(q => q.Part == 3).GroupBy(q => q.QuestionGroupId).ToList();
        Part4Groups = qs.Where(q => q.Part == 4).GroupBy(q => q.QuestionGroupId).ToList();
        Part5Questions = qs.Where(q => q.Part == 5).OrderBy(q => q.QuestionNumber).ToList();
        Part6Groups = qs.Where(q => q.Part == 6).GroupBy(q => q.PassageId).ToList();

        // Part 7 sets based on Passages and Questions
        var passages = Exam.Passages ?? [];
        var passageDict = passages.ToDictionary(p => p.Id);
        var part7Questions = qs.Where(q => q.Part == 7).ToList();

        var part7PassageGroups = part7Questions
            .Where(q => q.PassageId.HasValue)
            .GroupBy(q => q.PassageId!.Value);

        Part7Sets = [];
        foreach (var group in part7PassageGroups)
        {
            passageDict.TryGetValue(group.Key, out var passage);
            Part7Sets.Add(new ReadingSetViewModel
            {
                PassageId = group.Key,
                PassageContent = passage?.Content,
                ImageUrl = passage?.ImageUrl,
                Questions = group.OrderBy(q => q.QuestionNumber).ToList()
            });
        }

        Part7Orphans = part7Questions
            .Where(q => !q.PassageId.HasValue && !q.QuestionGroupId.HasValue)
            .OrderBy(q => q.QuestionNumber)
            .ToList();

        Part1Count = Part1Questions.Count;
        Part2Count = Part2Questions.Count;
        Part3Count = qs.Count(q => q.Part == 3);
        Part4Count = qs.Count(q => q.Part == 4);
        Part5Count = Part5Questions.Count;
        Part6Count = qs.Count(q => q.Part == 6);
        Part7Count = Part7Sets.Sum(s => s.Questions.Count) + Part7Orphans.Count;
        TotalQuestions = Part1Count + Part2Count + Part3Count + Part4Count + Part5Count + Part6Count + Part7Count;
    }
}

public class ExamPageHeroViewModel
{
    public required AdminExamDetail Exam { get; init; }
    public required int TotalQuestions { get; init; }
    public required bool IsReadOnly { get; init; }
    public string BreadcrumbCurrent { get; init; } = "Exam";
    public string HeroIcon { get; init; } = "quiz";
}
