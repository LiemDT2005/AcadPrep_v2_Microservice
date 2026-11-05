using Billing.API.Data;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Repositories;

public class OutboxRepository(BillingDbContext db) : IOutboxRepository
{
    public void Add(OutboxMessage message) => db.OutboxMessages.Add(message);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingForUpdateAsync(int take, CancellationToken ct) =>
        await db.OutboxMessages.Where(message => message.PublishedAt == null)
            .OrderBy(message => message.CreatedAt).ThenBy(message => message.Id).Take(take).ToListAsync(ct);
}
