using Notifications.API.Entities;

namespace Notifications.API.Repositories.Interfaces;

// One mail state per source event: never send the same reminder twice.
public interface IEmailDeliveryRepository
{
    // Tracked, so attempts and status can be saved.
    Task<EmailDelivery?> GetBySourceEventForUpdateAsync(Guid eventId, CancellationToken ct);
    // False when another delivery of the same event owns the mail (pending changes are discarded).
    Task<bool> TryAddAsync(EmailDelivery delivery, CancellationToken ct);
    Task UpdateAsync(EmailDelivery delivery, CancellationToken ct);
}
