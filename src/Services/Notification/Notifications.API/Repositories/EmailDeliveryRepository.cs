using Microsoft.EntityFrameworkCore;
using Notifications.API.Data;
using Notifications.API.Entities;
using Notifications.API.Repositories.Interfaces;

namespace Notifications.API.Repositories;

public class EmailDeliveryRepository(INotificationsDbContext db) : IEmailDeliveryRepository
{
    public Task<EmailDelivery?> GetBySourceEventForUpdateAsync(Guid eventId, CancellationToken ct) =>
        db.EmailDeliveries.FirstOrDefaultAsync(item => item.SourceEventId == eventId, ct);

    public async Task<bool> TryAddAsync(EmailDelivery delivery, CancellationToken ct)
    {
        db.EmailDeliveries.Add(delivery);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public Task UpdateAsync(EmailDelivery delivery, CancellationToken ct) => db.SaveChangesAsync(ct);
}
