namespace Consolidado.Api.Dtos;

/// <summary>Saldo diário consolidado do comerciante autenticado em uma data específica.</summary>
public sealed class SaldoDiarioResponse
{
    /// <summary>Data de referência do saldo.</summary>
    public DateOnly Data { get; set; }

    /// <summary>Soma de todos os créditos (entradas) registrados na data.</summary>
    public decimal TotalCreditos { get; set; }

    /// <summary>Soma de todos os débitos (saídas) registrados na data.</summary>
    public decimal TotalDebitos { get; set; }

    /// <summary>Saldo final do dia (<c>TotalCreditos - TotalDebitos</c>).</summary>
    public decimal SaldoFinal { get; set; }

    /// <summary>Data e hora (UTC) da última atualização desta projeção.</summary>
    public DateTimeOffset AtualizadoEm { get; set; }
}
