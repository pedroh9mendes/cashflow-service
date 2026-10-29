using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Lancamentos.Infrastructure;

/// <summary>
/// Drena periodicamente a tabela de Outbox e publica os eventos pendentes no RabbitMQ.
/// Se o broker estiver indisponível, as mensagens permanecem pendentes e são tentadas
/// novamente no próximo ciclo, sem impactar a disponibilidade de escrita de novos lançamentos
/// (ver ADR-002 e ADR-005).
/// </summary>
public sealed class OutboxPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<OutboxPublisherService> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(2);
    private const int MaxTentativasPorCiclo = 50;

    public OutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxPublisherService> logger)
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
                await PublicarPendentesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar o ciclo de publicação do Outbox. Nova tentativa no próximo ciclo.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task PublicarPendentesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LancamentosDbContext>();

        var pendentes = await dbContext.OutboxMessages
            .Where(m => m.PublicadoEm == null)
            .OrderBy(m => m.CriadoEm)
            .Take(MaxTentativasPorCiclo)
            .ToListAsync(cancellationToken);

        if (pendentes.Count == 0)
        {
            return;
        }

        IConnection? connection = null;
        IModel? channel = null;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = false
            };

            connection = factory.CreateConnection();
            channel = connection.CreateModel();
            RabbitMqTopology.Declarar(channel, _options);

            foreach (var mensagem in pendentes)
            {
                var body = Encoding.UTF8.GetBytes(mensagem.Payload);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.Type = mensagem.TipoEvento;

                channel.BasicPublish(_options.ExchangeName, _options.RoutingKey, properties, body);
                mensagem.MarcarComoPublicado();
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            LancamentosMetrics.EventosPublicadosOutbox.Inc(pendentes.Count);
            _logger.LogInformation("{Quantidade} evento(s) publicado(s) com sucesso no RabbitMQ.", pendentes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Não foi possível publicar eventos pendentes no RabbitMQ. Permanecerão na fila do Outbox para nova tentativa.");

            foreach (var mensagem in pendentes)
            {
                mensagem.RegistrarTentativaFalha();
            }

            LancamentosMetrics.EventosFalhaPublicacaoOutbox.Inc(pendentes.Count);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            channel?.Dispose();
            connection?.Dispose();
        }
    }
}
