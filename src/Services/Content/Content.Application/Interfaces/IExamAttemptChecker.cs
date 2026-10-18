namespace Content.Application.Interfaces;

public interface IExamAttemptChecker
{
    /// <summary>
    /// Checks the number of attempts for the given exam in the Exam Service via internal HTTP or event cache.
    /// </summary>
    Task<int> GetAttemptCountAsync(int examId, CancellationToken ct = default);
}
