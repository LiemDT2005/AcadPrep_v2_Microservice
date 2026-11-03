using Content.DTOs;

namespace Content.Repositories.Interfaces;

public interface IExamSeriesRepository
{
    Task<bool> ActiveExistsAsync(int id, CancellationToken ct = default);
    Task<List<ExamSeriesDto>> GetActiveAsync(CancellationToken ct = default);
}
