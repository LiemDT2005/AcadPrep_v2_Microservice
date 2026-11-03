using Exam.Entities.Enums;

namespace Exam.Entities;

public class AttemptAnswer
{
    public int AttemptId { get; set; }
    public int QuestionId { get; set; }
    public OptionLetter? SelectedOption { get; set; }
    public bool IsCorrect { get; set; }

    public virtual ExamAttempt ExamAttempt { get; set; } = null!;
    public virtual Question Question { get; set; } = null!;
}
