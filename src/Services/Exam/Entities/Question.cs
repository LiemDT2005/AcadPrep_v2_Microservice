using Exam.Common;
using Exam.Entities.Enums;

namespace Exam.Entities;

public class Question : BaseEntity<int>
{
    public int QuestionNumber { get; set; }
    public int Part { get; set; }
    public string? QuestionText { get; set; }
    
    public string? AudioUrl { get; set; }
    public int? AudioStartSecond { get; set; }
    public int? AudioEndSecond { get; set; }
    
    public string? ImageUrl { get; set; } 
    
    public OptionLetter CorrectOption { get; set; }
    public string? Explanation { get; set; }
    public int ExamId { get; set; }
    public int? PassageId { get; set; }
    public string? QuestionType { get; set; }
    public string? TopicTag { get; set; }
    public int? QuestionGroupId { get; set; }

    public virtual Exam Exam { get; set; } = null!;
    public virtual Passage? Passage { get; set; }
    public virtual QuestionGroup? QuestionGroup { get; set; }
    public virtual ICollection<QuestionOption> QuestionOptions { get; set; } = new List<QuestionOption>();
    public virtual ICollection<AttemptAnswer> AttemptAnswers { get; set; } = new List<AttemptAnswer>();
}
