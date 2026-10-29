namespace Shared.Contracts;

public sealed record LancamentoRegistrado(
    Guid LancamentoId,
    Guid ComercianteId,
    TipoLancamento Tipo,
    decimal Valor,
    DateOnly Data,
    DateTimeOffset RegistradoEm,
    string CorrelationId);

/// <summary>Natureza de um lançamento no fluxo de caixa.</summary>
public enum TipoLancamento
{
    /// <summary>Entrada de recurso no caixa (ex: venda, recebimento).</summary>
    Credito = 1,

    /// <summary>Saída de recurso do caixa (ex: pagamento a fornecedor, despesa).</summary>
    Debito = 2
}
