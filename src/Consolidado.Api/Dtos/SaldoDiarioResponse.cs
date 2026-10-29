namespace Consolidado.Api.Dtos;

public sealed class SaldoDiarioResponse
{
    public DateOnly Data { get; set; }
    public decimal TotalCreditos { get; set; }
    public decimal TotalDebitos { get; set; }
    public decimal SaldoFinal { get; set; }
    public DateTimeOffset AtualizadoEm { get; set; }
}
