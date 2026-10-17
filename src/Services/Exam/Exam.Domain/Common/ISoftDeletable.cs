namespace Exam.Domain.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    void SoftDelete();
}
