using Prometheus;

namespace Consolidado.Infrastructure;

public static class ConsolidadoMetrics
{
    public static readonly Counter EventosConsumidos = Metrics.CreateCounter(
        "consolidado_eventos_consumidos_total", "Total de eventos de lançamento consumidos da fila.");

    public static readonly Counter EventosDuplicadosDescartados = Metrics.CreateCounter(
        "consolidado_eventos_duplicados_total", "Total de eventos descartados por já terem sido processados (idempotência).");

    public static readonly Gauge FilaProfundidade = Metrics.CreateGauge(
        "consolidado_fila_profundidade", "Quantidade de mensagens pendentes na fila de lançamentos no momento da última checagem.");
}
