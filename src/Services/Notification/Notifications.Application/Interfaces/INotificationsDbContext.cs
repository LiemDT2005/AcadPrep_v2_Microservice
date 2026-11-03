using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;

namespace Notifications.Application.Interfaces;

public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }
    DbSet<UserNotificationState> UserNotificationStates { get; }
}
