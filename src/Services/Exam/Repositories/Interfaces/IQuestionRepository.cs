using Exam.Entities;

namespace Exam.Repositories.Interfaces;

public interface IQuestionRepository
{
    Task<bool> AnyForExamAsync(int examId, CancellationToken ct = default);
    Task<List<Question>> GetByExamIdAsync(int examId, CancellationToken ct = default);
    Task<List<Question>> GetPracticeCandidatesAsync(
        int examId, IReadOnlyCollection<int> parts, IReadOnlyCollection<string> tags, CancellationToken ct = default);
    Task<List<Question>> GetSessionQuestionsAsync(
        int examId, IReadOnlyCollection<int>? ids = null, CancellationToken ct = default);
}
