namespace Consolidado.Domain;

public interface ISaldoDiarioRepository
{
    Task<SaldoDiario?> BuscarAsync(Guid comercianteId, DateOnly data, CancellationToken cancellationToken);

    Task SalvarAsync(SaldoDiario saldoDiario, CancellationToken cancellationToken);

    /// <summary>
    /// Tenta salvar respeitando o token de concorrência otimista (RowVersion). Retorna false,
    /// sem lançar exceção, se outra atualização concorrente já alterou a mesma linha desde a
    /// leitura - o chamador decide se relê e reaplica (ver AplicarLancamentoService).
    /// </summary>
    Task<bool> TentarSalvarAsync(SaldoDiario saldoDiario, CancellationToken cancellationToken);
}
