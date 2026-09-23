using BuildingBlocks.Common.Events;

namespace BuildingBlocks.EventBus.Abstractions;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent;
    void Subscribe<T, TH>() where T : IntegrationEvent where TH : class, IIntegrationEventHandler<T>;
}

public interface IIntegrationEventHandler<in T> where T : IntegrationEvent
{
    Task HandleAsync(T @event, CancellationToken cancellationToken = default);
}
