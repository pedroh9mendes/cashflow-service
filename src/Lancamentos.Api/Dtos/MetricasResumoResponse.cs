namespace Lancamentos.Api.Dtos;

/// <summary>
/// Visão operacional agregada do serviço de Lançamentos, usada pelo painel de observabilidade
/// interno (não é dado de negócio de um tenant específico — é contagem agregada entre todos os
/// comerciantes, por isso não exige autenticação de tenant).
/// </summary>
public sealed class MetricasResumoResponse
{
    /// <summary>Quantidade de lançamentos registrados na data de hoje, entre todos os comerciantes.</summary>
    public int LancamentosHoje { get; set; }

    /// <summary>Quantidade total de lançamentos já registrados na plataforma, desde o início.</summary>
    public long LancamentosTotal { get; set; }

    /// <summary>Quantidade de comerciantes distintos com ao menos um lançamento registrado.</summary>
    public int ComerciantesAtivos { get; set; }

    /// <summary>
    /// Quantidade de mensagens pendentes de publicação no Outbox (ver ADR-005) — um valor
    /// persistentemente alto indica falha na publicação de eventos para o RabbitMQ.
    /// </summary>
    public int OutboxPendentes { get; set; }

    /// <summary>Estado atual do circuit breaker de contingência (Camada 2 de resiliência).</summary>
    public string EstadoCircuitBreaker { get; set; } = string.Empty;
}
