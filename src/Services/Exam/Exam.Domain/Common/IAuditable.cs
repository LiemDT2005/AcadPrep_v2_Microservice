namespace Exam.Domain.Common;

public interface IAuditable
{
    DateTime CreatedAt { get; }
    DateTime? LastModifiedAt { get; }
}
