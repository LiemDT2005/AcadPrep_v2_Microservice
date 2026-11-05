namespace Billing.API.Services.Interfaces;

public interface IOutboxPublisher
{
    // Returns the number of events published in this run.
    Task<int> RunOnceAsync(CancellationToken ct);
}
