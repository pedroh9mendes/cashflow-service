namespace Lancamentos.Domain;

public interface ILancamentoRepository
{
    Task AdicionarAsync(Lancamento lancamento, CancellationToken cancellationToken);

    Task<IReadOnlyList<Lancamento>> ListarPorComerciantEDataAsync(
        Guid comercianteId, DateOnly data, CancellationToken cancellationToken);

    Task<bool> ExisteAsync(Guid lancamentoId, CancellationToken cancellationToken);
}
