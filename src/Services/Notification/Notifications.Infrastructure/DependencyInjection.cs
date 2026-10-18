using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Persistence;

namespace Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddScoped<INotificationsDbContext>(provider => provider.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<INotificationReadStateStore, NotificationReadStateStore>();
        services.AddScoped<INotificationWriter, NotificationWriter>();
        return services;
    }
}
