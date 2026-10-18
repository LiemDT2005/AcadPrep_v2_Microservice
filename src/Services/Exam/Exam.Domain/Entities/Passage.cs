using Exam.Domain.Common;

namespace Exam.Domain.Entities;

public class Passage : BaseEntity<int>
{
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public string? Explanation { get; set; }
    public int DisplayOrder { get; set; } = 1;

    public int ExamId { get; set; }
    public int? QuestionGroupId { get; set; }

    public virtual Exam Exam { get; set; } = null!;
    public virtual QuestionGroup? QuestionGroup { get; set; }
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}
