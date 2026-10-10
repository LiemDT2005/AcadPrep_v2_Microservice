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
        services.AddDbContext<ExamDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddScoped<IExamDbContext>(sp => sp.GetRequiredService<ExamDbContext>());
        services.AddEventBus(configuration);
        return services;
    }
}
