using System.Text;
using RabbitMQ.Client;

namespace Workflow.Infrastructure;

public interface IBusinessPublisher
{
    Task Publish(BusinessCommand command, CancellationToken cancellation);
}

public sealed class RabbitMqOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 5672;
    public string User { get; init; } = "";
    public string Password { get; init; } = "";
    public string VirtualHost { get; init; } = "workflow-demo";
    public string Exchange { get; init; } = "workflow.business";
    public string Queue { get; init; } = "workflow.business.commands";
    public string RoutingKey { get; init; } = "business.command";
    public bool Tls { get; init; }
}

// Infrastructure only. Broker acknowledgement is not a business result.
public sealed class RabbitMqPublisher(RabbitMqOptions options) : IBusinessPublisher, IAsyncDisposable
{
    // A channel cannot publish concurrently safely. Serialize short confirmed sends on one reusable channel.
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public async Task Publish(BusinessCommand command, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            if (channel?.IsOpen != true)
            {
                await Reset();
                if (string.IsNullOrWhiteSpace(options.User) || string.IsNullOrWhiteSpace(options.Password) ||
                    (!options.Tls && options.Host is not "127.0.0.1" and not "localhost" and not "::1"))
                    throw new InvalidOperationException("RabbitMQ credentials and secure transport are required");
                var factory = new ConnectionFactory
                {
                    HostName = options.Host, Port = options.Port, UserName = options.User,
                    Password = options.Password, VirtualHost = options.VirtualHost,
                    AutomaticRecoveryEnabled = false,
                    Ssl = new SslOption { Enabled = options.Tls, ServerName = options.Host }
                };
                connection = await factory.CreateConnectionAsync(cancellation);
                channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), cancellation);
                await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellation);
                await channel.QueueDeclareAsync(options.Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellation);
                await channel.QueueBindAsync(options.Queue, options.Exchange, options.RoutingKey, cancellationToken: cancellation);
            }
            var properties = new BasicProperties
            {
                Persistent = true, ContentType = "application/json", Type = "business.command.v1",
                MessageId = command.Request.OperationId, CorrelationId = command.Request.OperationId
            };
            // Tracking makes nack / mandatory basic.return fail this await. No false successful dispatch.
            await channel.BasicPublishAsync(options.Exchange, options.RoutingKey, mandatory: true,
                properties, Encoding.UTF8.GetBytes(Json.Write(command)), cancellation);
        }
        finally { gate.Release(); }
    }

    private async Task Reset()
    {
        if (channel != null) await channel.DisposeAsync();
        if (connection != null) await connection.DisposeAsync();
        channel = null; connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try { await Reset(); }
        finally { gate.Release(); }
    }
}
