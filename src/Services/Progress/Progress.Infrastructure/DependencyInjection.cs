using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus;
using BuildingBlocks.EventBus.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Progress.Application.Interfaces;
using Progress.Infrastructure.Caching;
using Progress.Infrastructure.Messaging;
using Progress.Infrastructure.Persistence;

namespace Progress.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProgressDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddScoped<IProgressDbContext>(sp => sp.GetRequiredService<ProgressDbContext>());

        var redis = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(opt => opt.Configuration = redis);
        services.AddSingleton<IProgressCache, RedisProgressCache>();

        services.AddEventBus(configuration);
        services.AddScoped<IIntegrationEventHandler<ExamCompletedEvent>, ExamCompletedHandler>();
        services.AddScoped<ExamCompletedHandler>();

        return services;
    }

    public static void SubscribeEvents(this IServiceProvider sp)
    {
        var bus = sp.GetRequiredService<IEventBus>();
        bus.Subscribe<ExamCompletedEvent, ExamCompletedHandler>();
    }
}
