namespace Exam.Domain.Common;

public abstract class BaseEntity<TKey> where TKey : notnull
{
    public TKey Id { get; set; } = default!;
}
