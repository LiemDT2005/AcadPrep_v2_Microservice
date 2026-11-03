namespace Exam.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    void SoftDelete();
}
