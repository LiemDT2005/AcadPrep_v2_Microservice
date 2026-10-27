using Learning.Application.DTOs;
namespace Learning.Application.Interfaces;
public interface IExamStatisticsSource
{
    Task<List<ExamStatsItemDto>> GetAsync(CancellationToken ct);
}
