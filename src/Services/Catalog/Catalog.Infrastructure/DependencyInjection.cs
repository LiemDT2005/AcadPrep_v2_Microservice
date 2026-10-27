using Catalog.Application.Interfaces;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=catalog.db";
        var isSqlServer = string.Equals(configuration["UseSqlServer"], "true", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("Data Source=.", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<CatalogDbContext>(opt =>
        {
            if (isSqlServer)
                opt.UseSqlServer(connectionString);
            else
                opt.UseSqlite(connectionString);
        });
        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
        return services;
    }
}
