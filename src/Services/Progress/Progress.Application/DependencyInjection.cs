using Progress.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Progress.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProgressService>();
        return services;
    }
}
