using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus;
using BuildingBlocks.EventBus.Abstractions;
using Learning.Application.Interfaces;
using Learning.Infrastructure.BackgroundJobs;
using Learning.Infrastructure.Caching;
using Learning.Infrastructure.Messaging;
using Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=learning.db";
        var isSqlServer = string.Equals(configuration["UseSqlServer"], "true", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("Data Source=.", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<LearningDbContext>(opt =>
        {
            if (isSqlServer)
                opt.UseSqlServer(connectionString);
            else
                opt.UseSqlite(connectionString);
        });
        services.AddScoped<ILearningDbContext>(sp => sp.GetRequiredService<LearningDbContext>());

        var redis = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(opt => opt.Configuration = redis);
        services.AddSingleton<ILearningCache, RedisLearningCache>();

        services.AddEventBus(configuration);
        services.AddScoped<IIntegrationEventHandler<ExamCompletedEvent>, TestSubmittedHandler>();
        services.AddScoped<IIntegrationEventHandler<TestSubmittedEvent>, TestSubmittedHandler>();
        services.AddScoped<TestSubmittedHandler>();

        services.AddHostedService<StudyStreakResetWorker>();

        return services;
    }

    public static void SubscribeEvents(this IServiceProvider sp)
    {
        var bus = sp.GetRequiredService<IEventBus>();
        bus.Subscribe<ExamCompletedEvent, TestSubmittedHandler>();
        bus.Subscribe<TestSubmittedEvent, TestSubmittedHandler>();
    }
}
