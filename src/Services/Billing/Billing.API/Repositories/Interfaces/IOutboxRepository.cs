using Billing.API.Entities;

namespace Billing.API.Repositories.Interfaces;

// Transactional outbox: events are added in the same unit of work as the change they describe.
public interface IOutboxRepository
{
    void Add(OutboxMessage message);
    // Unpublished messages in creation order (CreatedAt, then Id); tracked so publishing state can be saved.
    Task<IReadOnlyList<OutboxMessage>> GetPendingForUpdateAsync(int take, CancellationToken ct);
}
