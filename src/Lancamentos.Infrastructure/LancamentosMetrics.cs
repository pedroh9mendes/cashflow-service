using Prometheus;

namespace Lancamentos.Infrastructure;

/// <summary>
/// Métricas de negócio expostas via Prometheus, conforme docs/observability.md.
/// </summary>
public static class LancamentosMetrics
{
    public static readonly Counter LancamentosRegistrados = Metrics.CreateCounter(
        "lancamentos_registrados_total", "Total de lançamentos registrados com sucesso.");

    public static readonly Counter EventosPublicadosOutbox = Metrics.CreateCounter(
        "outbox_eventos_publicados_total", "Total de eventos publicados pelo processo de Outbox.");

    public static readonly Counter EventosFalhaPublicacaoOutbox = Metrics.CreateCounter(
        "outbox_eventos_falha_publicacao_total", "Total de falhas de publicação de eventos do Outbox.");

    public static readonly Gauge CircuitBreakerEstado = Metrics.CreateGauge(
        "circuit_breaker_estado", "Estado do circuit breaker de contingência (0 = Normal, 1 = Contingência).");
}
