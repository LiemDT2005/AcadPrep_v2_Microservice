using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Notifications.Application.Services;

namespace Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<NotificationQueryService>();
        services.AddScoped<NotificationCommandService>();
        services.AddScoped<NotificationAdminService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
