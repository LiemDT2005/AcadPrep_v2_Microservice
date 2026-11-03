using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notifications.Infrastructure.Persistence;

// EF tooling can generate migrations without starting the API, loading JWT keys or opening a connection.
public class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlServer("Server=localhost;Database=AcadPrep_NotificationDb;Integrated Security=True;Encrypt=True")
            .Options;
        return new NotificationsDbContext(options);
    }
}
