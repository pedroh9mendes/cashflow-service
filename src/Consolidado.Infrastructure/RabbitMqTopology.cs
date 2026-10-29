using RabbitMQ.Client;

namespace Consolidado.Infrastructure;

/// <summary>
/// Declara a mesma topologia definida em Lancamentos.Infrastructure.RabbitMqTopology -
/// exchange, fila principal e dead-letter queue devem ser idênticas em nome para que o
/// consumidor leia da fila alimentada pelo publisher do Outbox (ver ADR-002).
/// </summary>
public static class RabbitMqTopology
{
    public static void Declarar(IModel channel, RabbitMqOptions options)
    {
        channel.ExchangeDeclare(options.ExchangeName, ExchangeType.Direct, durable: true);

        channel.QueueDeclare(options.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false);

        var queueArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", "" },
            { "x-dead-letter-routing-key", options.DeadLetterQueueName }
        };

        channel.QueueDeclare(options.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs);
        channel.QueueBind(options.QueueName, options.ExchangeName, options.RoutingKey);
    }
}
