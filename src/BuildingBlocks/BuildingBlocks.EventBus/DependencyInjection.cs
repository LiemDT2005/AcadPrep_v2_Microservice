using BuildingBlocks.EventBus.Abstractions;
using BuildingBlocks.EventBus.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.EventBus;

public static class DependencyInjection
{
    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        return services;
    }
}
