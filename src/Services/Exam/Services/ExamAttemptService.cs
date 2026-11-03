using AutoMapper;
using Exam.DTOs;
using Exam.Repositories.Interfaces;
using Exam.Services.Interfaces;

namespace Exam.Services;

public class ExamAttemptService(IExamAttemptRepository attempts, IMapper mapper) : IExamAttemptService
{
    public Task<int> GetAttemptCountAsync(int examId, CancellationToken ct = default) =>
        attempts.CountForExamAsync(examId, ct);

    public async Task<List<UserAttemptHistoryDto>> GetHistoryAsync(int examId, string userId, CancellationToken ct = default) =>
        mapper.Map<List<UserAttemptHistoryDto>>(await attempts.GetHistoryAsync(examId, userId, ct));
}
