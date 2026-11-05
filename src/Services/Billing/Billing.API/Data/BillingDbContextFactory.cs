using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Billing.API.Data;

// EF tooling can generate migrations without starting the API, loading JWT keys or opening a connection.
// The default matches appsettings.json; ConnectionStrings__Default overrides it.
public class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public const string DefaultConnection =
        "Server=localhost;Database=AcadPrep_BillingDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    public BillingDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? DefaultConnection : connection)
            .Options;
        return new BillingDbContext(options);
    }
}
