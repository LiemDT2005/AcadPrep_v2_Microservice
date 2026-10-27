using Learning.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Learning.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LeaderboardService>();
        services.AddScoped<PerformanceService>();
        services.AddScoped<GamificationService>();
        services.AddScoped<VocabularyService>();
        services.AddScoped<LearningAdminService>();

        return services;
    }
}
