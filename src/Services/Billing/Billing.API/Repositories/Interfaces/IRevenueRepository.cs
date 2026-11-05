namespace Billing.API.Repositories.Interfaces;

public record PaidAmount(DateTime PaidAt, decimal Amount);

public record OrderStatusCounts(int Paid, int Failed, int Pending, int Expired);

public interface IRevenueRepository
{
    Task<IReadOnlyList<PaidAmount>> GetPaidAsync(DateTime fromUtc, DateTime toExclusiveUtc, CancellationToken ct);
    Task<OrderStatusCounts> CountCreatedAsync(DateTime fromUtc, DateTime toExclusiveUtc, DateTime now, CancellationToken ct);
}
