using BuildingBlocks.Common.Results;

namespace Exam.Services.Interfaces;

public interface IExamQuotaPolicy
{
    Task<Result<bool>> CanStartAsync(string userId, bool practice, CancellationToken ct);
}
