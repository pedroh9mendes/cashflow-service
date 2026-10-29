namespace Consolidado.Api.Dtos;

/// <summary>
/// Visão operacional agregada do serviço de Consolidado, usada pelo painel interno de
/// observabilidade (não é dado de negócio de um tenant específico — é contagem agregada entre
/// todos os comerciantes, por isso não exige autenticação de tenant).
/// </summary>
public sealed class MetricasResumoResponse
{
    /// <summary>Quantidade de comerciantes com saldo diário calculado para a data de hoje.</summary>
    public int ComerciantesComSaldoHoje { get; set; }

    /// <summary>Quantidade total de projeções de saldo diário já calculadas, desde o início.</summary>
    public long ProjecoesTotal { get; set; }

    /// <summary>
    /// Quantidade total de eventos de lançamento já processados pelo consumidor da fila,
    /// usada como indicador de idempotência (cada evento é processado uma única vez).
    /// </summary>
    public long EventosProcessadosTotal { get; set; }
}
