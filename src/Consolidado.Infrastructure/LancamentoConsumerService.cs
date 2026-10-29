using System.Text;
using System.Text.Json;
using Consolidado.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;
using Shared.Security;

namespace Consolidado.Infrastructure;

/// <summary>
/// Consome eventos LancamentoRegistrado da fila e atualiza a projeção de saldo diário
/// (read model do CQRS, ver ADR-004). Reconecta automaticamente se o broker estiver
/// indisponível, sem impactar a disponibilidade do serviço de Lançamentos (ver ADR-002).
/// </summary>
public sealed class LancamentoConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<LancamentoConsumerService> _logger;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    public LancamentoConsumerService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<LancamentoConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Falha na conexão com o RabbitMQ. Nova tentativa em {Delay}s.", ReconnectDelay.TotalSeconds);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        RabbitMqTopology.Declarar(channel, _options);
        channel.BasicQos(prefetchSize: 0, prefetchCount: 20, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.Received += async (_, eventArgs) =>
        {
            try
            {
                var payload = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                var evento = JsonSerializer.Deserialize<LancamentoRegistrado>(payload);

                if (evento is not null)
                {
                    using var scope = _scopeFactory.CreateScope();

                    // Define o tenant do evento no contexto ambiente da unidade de trabalho
                    // (esta mensagem) antes de resolver o DbContext, habilitando o RLS do
                    // SQL Server via SESSION_CONTEXT mesmo fora de uma requisição HTTP
                    // (ver ADR-006, seção "Exceção justificada: consumidor de eventos do Consolidado").
                    var ambientTenantContextAccessor = scope.ServiceProvider.GetRequiredService<AmbientTenantContextAccessor>();
                    ambientTenantContextAccessor.Definir(evento.ComercianteId);

                    var aplicarLancamentoService = scope.ServiceProvider.GetRequiredService<AplicarLancamentoService>();

                    var aplicado = await aplicarLancamentoService.AplicarAsync(evento, stoppingToken);

                    ConsolidadoMetrics.EventosConsumidos.Inc();

                    if (!aplicado)
                    {
                        ConsolidadoMetrics.EventosDuplicadosDescartados.Inc();
                    }
                }

                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar evento da fila. Encaminhando para dead-letter queue.");
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
        };

        channel.BasicConsume(_options.QueueName, autoAck: false, consumer);

        while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
        {
            ConsolidadoMetrics.FilaProfundidade.Set(channel.MessageCount(_options.QueueName));
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
