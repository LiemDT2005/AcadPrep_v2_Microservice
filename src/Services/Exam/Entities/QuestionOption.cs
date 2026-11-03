using Exam.Common;
using Exam.Entities.Enums;

namespace Exam.Entities;

public class QuestionOption : BaseEntity<int>
{
    public int QuestionId { get; set; }
    public OptionLetter OptionLetter { get; set; }
    public string OptionText { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;
}
