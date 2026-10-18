using Content.Domain.Enums;

namespace Content.Domain.Entities;

public class Exam
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string ExamType { get; set; } = "Full Test";
    public string? Description { get; set; }
    public int Duration { get; set; } = 120;
    public string? AudioUrl { get; set; }
    public ExamStatus Status { get; set; } = ExamStatus.Draft;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }
    public string? CreatedBy { get; set; }

    public int ExamSeriesId { get; set; }
    public virtual ExamSeries ExamSeries { get; set; } = null!;

    public virtual ICollection<Passage> Passages { get; set; } = new List<Passage>();
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    public virtual ICollection<QuestionGroup> QuestionGroups { get; set; } = new List<QuestionGroup>();

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        Status = ExamStatus.Hidden;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        Status = ExamStatus.Published;
    }
}
