namespace Content.Entities;

public class QuestionGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? AudioUrl { get; set; }
    public int? AudioStartSecond { get; set; }
    public int? AudioEndSecond { get; set; }
    public string? ImageUrl { get; set; }
    public string? Explanation { get; set; }
    public int ExamId { get; set; }

    public virtual Exam Exam { get; set; } = null!;
    public virtual ICollection<Passage> Passages { get; set; } = new List<Passage>();
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}
