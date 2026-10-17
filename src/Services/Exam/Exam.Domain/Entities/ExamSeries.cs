using Exam.Domain.Common;

namespace Exam.Domain.Entities;

public class ExamSeries : BaseEntity<int>, IAuditable, ISoftDeletable
{
    public string Name { get; set; } = null!;
    public int Year { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }
    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public void SoftDelete() => IsDeleted = true;
}
