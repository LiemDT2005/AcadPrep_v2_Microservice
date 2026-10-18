using Notifications.Domain.Entities;

namespace Notifications.Application.Interfaces;

public interface INotificationWriter
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
}
