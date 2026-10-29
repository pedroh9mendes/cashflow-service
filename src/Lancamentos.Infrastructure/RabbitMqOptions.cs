namespace Lancamentos.Infrastructure;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "fluxo-caixa.eventos";
    public string QueueName { get; set; } = "consolidado.lancamento-registrado";
    public string RoutingKey { get; set; } = "lancamento.registrado";
    public string DeadLetterQueueName { get; set; } = "consolidado.lancamento-registrado.dlq";
}
