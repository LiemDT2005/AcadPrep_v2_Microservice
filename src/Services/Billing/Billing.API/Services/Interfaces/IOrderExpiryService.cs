namespace Billing.API.Services.Interfaces;

public interface IOrderExpiryService
{
    // Returns the number of Pending orders marked Expired.
    Task<int> RunOnceAsync(CancellationToken ct);
}
