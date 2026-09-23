namespace Exam.Domain.Entities;

public class PracticeExam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Part { get; set; } = "Part5"; // Part1..Part7 mini demo
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; } = 10;
    public List<Question> Questions { get; set; } = [];
}

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PracticeExamId { get; set; }
    public PracticeExam? PracticeExam { get; set; }
    public string Stem { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = "A"; // A/B/C/D
    public int Order { get; set; }
}

public class ExamAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PracticeExamId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public int Score { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public string AnswersJson { get; set; } = "[]";
}
