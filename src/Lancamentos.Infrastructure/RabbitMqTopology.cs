using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Lancamentos.Infrastructure;

/// <summary>
/// Declara a topologia da fila: exchange, fila principal durável e dead-letter queue,
/// para mensagens que falham repetidamente no processamento pelo consumidor (ver docs/nfr.md).
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
