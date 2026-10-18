using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

public class NotificationWriter(NotificationsDbContext db) : INotificationWriter
{
    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }
}
