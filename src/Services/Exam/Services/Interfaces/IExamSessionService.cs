using BuildingBlocks.Common.Results;
using Exam.DTOs;

namespace Exam.Services.Interfaces;

public interface IExamSessionService
{
    Task<Result<int>> StartFullTestAsync(int examId, string userId, CancellationToken ct = default);
    Task<Result<int>> StartPracticeAsync(int examId, string userId, StartPracticeRequest request, CancellationToken ct = default);
    Task<ExamSessionDto?> GetFullTestAsync(int id, string userId, CancellationToken ct = default);
    Task<ExamSessionDto?> GetPracticeAsync(int id, string userId, CancellationToken ct = default);
}
