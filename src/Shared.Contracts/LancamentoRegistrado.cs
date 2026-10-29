namespace Shared.Contracts;

public sealed record LancamentoRegistrado(
    Guid LancamentoId,
    Guid ComercianteId,
    TipoLancamento Tipo,
    decimal Valor,
    DateOnly Data,
    DateTimeOffset RegistradoEm,
    string CorrelationId);

public enum TipoLancamento
{
    Credito = 1,
    Debito = 2
}
