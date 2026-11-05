using Learning.API.DTOs;

namespace Learning.API.Messaging;

public interface IExamStatisticsSource
{
    Task<List<ExamStatsItemDto>> GetAsync(CancellationToken ct);
}
