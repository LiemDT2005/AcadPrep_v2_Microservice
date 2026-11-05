using Microsoft.EntityFrameworkCore;
using Notifications.API.Data;
using Notifications.API.Repositories.Interfaces;
using Notifications.API.Services.Interfaces;
using Notifications.API.Entities;

namespace Notifications.API.Data;

public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options), INotificationsDbContext
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserNotificationState> UserNotificationStates => Set<UserNotificationState>();
    public DbSet<EmailDelivery> EmailDeliveries => Set<EmailDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
