using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Notifications.API.Entities;

namespace Notifications.API.Data;

// What repositories need from the DbContext (same idea as Exam's IExamDbContext); tests swap in SQLite through it.
public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }
    DbSet<UserNotificationState> UserNotificationStates { get; }
    DbSet<EmailDelivery> EmailDeliveries { get; }
    ChangeTracker ChangeTracker { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
