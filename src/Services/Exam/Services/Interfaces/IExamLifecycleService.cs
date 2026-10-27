using BuildingBlocks.Common.Results;
using Exam.DTOs;

namespace Exam.Services.Interfaces;

public interface IExamLifecycleService
{
    Task<Result<int>> StartAsync(int examId, string userId, StartPracticeRequest? practice, CancellationToken ct);
    Task CaptureAsync(int id, string mode, string userId, CancellationToken ct);
    Task<ExamSessionDto?> GetAsync(int id, string mode, string userId, CancellationToken ct);
    Task<Result<bool>> SaveAsync(int id, string mode, string userId, SaveSessionRequest request, CancellationToken ct);
    Task<Result<AttemptReviewDto>> SubmitAsync(int id, string mode, string userId, SaveSessionRequest request, CancellationToken ct);
    Task<AttemptReviewDto?> ReviewAsync(int id, string mode, string userId, CancellationToken ct);
    Task<StudyHistoryResultDto> HistoryAsync(string userId, CancellationToken ct);
    Task<ExamAttemptsResultDto> AttemptsAsync(string userId, CancellationToken ct);
    Task<IncorrectAnswersResultDto> IncorrectAsync(string userId, CancellationToken ct);
}
