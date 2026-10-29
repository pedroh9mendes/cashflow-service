namespace Contingencia.ClienteExemplo;

/// <summary>
/// Espelha os valores de Shared.Contracts.TipoLancamento (Credito = 1, Debito = 2), sem
/// referenciar o projeto diretamente - este cliente simula um dispositivo de borda que não
/// compartilha binários com o backend, apenas o contrato HTTP.
/// </summary>
public enum TipoLancamentoLocal
{
    Credito = 1,
    Debito = 2
}

public sealed record RegistroLocal(
    Guid IdOrigem,
    TipoLancamentoLocal Tipo,
    decimal Valor,
    string Descricao,
    DateOnly Data,
    bool Sincronizado);
