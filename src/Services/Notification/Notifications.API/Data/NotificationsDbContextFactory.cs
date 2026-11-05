using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notifications.API.Data;

// EF tooling can generate migrations without starting the API, loading JWT keys or opening a connection.
// The v2 database is AcadPrep_NotificationsDb (same name as Compose); the legacy AcadPrep_NotificationDb has
// a different, non-migrated schema and must not be targeted. ConnectionStrings__Default overrides the default.
public class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public const string DefaultConnection =
        "Server=localhost;Database=AcadPrep_NotificationsDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? DefaultConnection : connection)
            .Options;
        return new NotificationsDbContext(options);
    }
}
