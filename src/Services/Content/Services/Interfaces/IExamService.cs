using BuildingBlocks.Common.Pagination;
using BuildingBlocks.Common.Results;
using Content.DTOs;

namespace Content.Services.Interfaces;

public interface IExamService
{
    Task<Result<GetExamListResponse>> GetExamListAsync(
        string? search, string? seriesName, int? year,
        int pageIndex = PaginationConstants.DefaultPageIndex,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken ct = default);

    Task<Result<GetExamDetailDto>> GetExamDetailAsync(int id, CancellationToken ct = default);
}
