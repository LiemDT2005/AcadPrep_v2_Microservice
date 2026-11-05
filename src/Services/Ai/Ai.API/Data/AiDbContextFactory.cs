using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ai.API.Data;

// EF tooling can generate migrations without starting the API, loading JWT keys or opening a connection.
// The default matches appsettings.json (the legacy AcadPrep_AiDb must not be targeted);
// ConnectionStrings__Default overrides it.
public class AiDbContextFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public const string DefaultConnection =
        "Server=localhost;Database=AcadPrep_AiServiceDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";

    public AiDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connection) ? DefaultConnection : connection)
            .Options;
        return new AiDbContext(options);
    }
}
