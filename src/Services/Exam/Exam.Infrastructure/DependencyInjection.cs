using BuildingBlocks.EventBus;
using Exam.Application.Interfaces;
using Exam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exam.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=exam.db";
        var isSqlServer = string.Equals(configuration["UseSqlServer"], "true", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("Data Source=.", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<ExamDbContext>(opt =>
        {
            if (isSqlServer)
                opt.UseSqlServer(connectionString);
            else
                opt.UseSqlite(connectionString);
        });
        services.AddScoped<IExamDbContext>(sp => sp.GetRequiredService<ExamDbContext>());
        services.AddEventBus(configuration);
        return services;
    }
}
