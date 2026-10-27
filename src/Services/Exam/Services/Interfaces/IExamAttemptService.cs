using Exam.DTOs;

namespace Exam.Services.Interfaces;

public interface IExamAttemptService
{
    Task<int> GetAttemptCountAsync(int examId, CancellationToken ct = default);
    Task<List<UserAttemptHistoryDto>> GetHistoryAsync(int examId, string userId, CancellationToken ct = default);
}
