using BuildingBlocks.Common.Results;
using Exam.Services.Interfaces;

namespace Exam.Services;

// Replace this registration with Billing's adapter when the team agrees its quota/reservation contract.
public class PendingBillingQuotaPolicy : IExamQuotaPolicy
{
    public Task<Result<bool>> CanStartAsync(string userId, bool practice, CancellationToken ct) =>
        Task.FromResult(Result<bool>.Success(true));
}
