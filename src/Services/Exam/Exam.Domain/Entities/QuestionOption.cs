using Exam.Domain.Common;
using Exam.Domain.Enums;

namespace Exam.Domain.Entities;

public class QuestionOption : BaseEntity<int>
{
    public int QuestionId { get; set; }
    public OptionLetter OptionLetter { get; set; }
    public string OptionText { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;
}
