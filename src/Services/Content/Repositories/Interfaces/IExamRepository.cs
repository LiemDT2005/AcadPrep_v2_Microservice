using Content.Common;
using Content.DTOs;
using Content.Entities;

namespace Content.Repositories.Interfaces;

public interface IExamRepository
{
    Task<List<string>> GetSeriesNamesAsync(CancellationToken ct = default);
    Task<List<int>> GetSeriesYearsAsync(CancellationToken ct = default);
    Task<PaginatedList<GetExamDto>> GetPublishedPageAsync(
        string? search, string? seriesName, int? year, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<Exam?> GetPublishedByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResultDto<ExamListItemDto>> GetPagedAsync(
        string? keyword, string? status, string? examType, int page, int pageSize, CancellationToken ct = default);
    Task<Exam?> GetDetailByIdAsync(int id, CancellationToken ct = default);
    Task<Exam?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(Exam exam, CancellationToken ct = default);
    Task UpdateAsync(Exam exam, CancellationToken ct = default);
    Task DeleteAsync(Exam exam, CancellationToken ct = default);
}
