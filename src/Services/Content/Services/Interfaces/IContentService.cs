using BuildingBlocks.Common.Results;
using Content.DTOs;

namespace Content.Services.Interfaces;

public interface IContentService
{
    Task<Result<PagedResultDto<ExamListItemDto>>> GetPagedExamsAsync(
        string? keyword,
        string? status,
        string? examType,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<ExamDetailDto>> GetExamByIdAsync(int id, CancellationToken ct = default);

    Task<Result<ExamDetailDto>> CreateExamAsync(CreateExamDto dto, CancellationToken ct = default);

    Task<Result> UpdateExamAsync(int id, UpdateExamDto dto, CancellationToken ct = default);

    Task<Result> ChangeStatusAsync(int id, string status, CancellationToken ct = default);

    Task<Result<DeleteExamResultDto>> DeleteExamAsync(int id, CancellationToken ct = default);

    Task<Result> RestoreExamAsync(int id, CancellationToken ct = default);

    Task<Result<List<ExamSeriesDto>>> GetExamSeriesAsync(CancellationToken ct = default);
}
