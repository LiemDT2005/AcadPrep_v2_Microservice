using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BuildingBlocks.EventBus.RabbitMQ;

public class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly ConcurrentDictionary<string, List<Type>> _handlers = new();
    private readonly ConcurrentDictionary<string, Type> _eventTypes = new();
    private IConnection? _connection;
    private IModel? _channel;
    private bool _disposed;

    public RabbitMqEventBus(
        IOptions<RabbitMqOptions> options,
        IServiceProvider serviceProvider,
        ILogger<RabbitMqEventBus> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
        TryConnect();
    }

    private void TryConnect()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = true
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true);
            _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", _options.Host, _options.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ not available yet. Events will be logged locally until broker is up.");
        }
    }

    public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        var eventName = typeof(T).Name;
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));

        if (_channel is null || _connection is null || !_connection.IsOpen)
        {
            TryConnect();
        }

        if (_channel is null || _connection is null || !_connection.IsOpen)
        {
            _logger.LogWarning("Publish skipped (no RabbitMQ). Event={Event} Payload={Payload}",
                eventName, Encoding.UTF8.GetString(body));
            return Task.CompletedTask;
        }

        var props = _channel.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2;
        props.MessageId = @event.Id.ToString();

        _channel.BasicPublish(_options.ExchangeName, eventName, props, body);
        _logger.LogInformation("Published {Event} Id={Id}", eventName, @event.Id);
        return Task.CompletedTask;
    }

    public void Subscribe<T, TH>() where T : IntegrationEvent where TH : class, IIntegrationEventHandler<T>
    {
        var eventName = typeof(T).Name;
        _eventTypes[eventName] = typeof(T);
        var handlers = _handlers.GetOrAdd(eventName, _ => []);
        if (!handlers.Contains(typeof(TH)))
            handlers.Add(typeof(TH));

        if (_channel is null || _connection is null || !_connection.IsOpen)
            TryConnect();

        if (_channel is null) return;

        var queueName = $"toeic_{eventName}_{typeof(TH).Name}";
        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(queueName, _options.ExchangeName, eventName);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnReceivedAsync;
        _channel.BasicConsume(queueName, autoAck: false, consumer);
        _logger.LogInformation("Subscribed {Handler} to {Event}", typeof(TH).Name, eventName);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var eventName = args.RoutingKey;
        var message = Encoding.UTF8.GetString(args.Body.ToArray());

        try
        {
            if (!_handlers.TryGetValue(eventName, out var handlerTypes) ||
                !_eventTypes.TryGetValue(eventName, out var eventType))
            {
                _channel?.BasicAck(args.DeliveryTag, false);
                return;
            }

            var @event = JsonSerializer.Deserialize(message, eventType);
            using var scope = _serviceProvider.CreateScope();
            foreach (var handlerType in handlerTypes)
            {
                var handler = scope.ServiceProvider.GetService(handlerType);
                if (handler is null) continue;
                var method = handlerType.GetMethod("HandleAsync");
                if (method is null) continue;
                var task = (Task?)method.Invoke(handler, [@event, CancellationToken.None]);
                if (task is not null) await task;
            }

            _channel?.BasicAck(args.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {Event}: {Message}", eventName, message);
            _channel?.BasicNack(args.DeliveryTag, false, requeue: true);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
