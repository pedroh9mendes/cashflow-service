namespace Consolidado.Domain;

public interface IEventoProcessadoRepository
{
    Task<bool> JaProcessadoAsync(Guid lancamentoId, CancellationToken cancellationToken);

    Task RegistrarAsync(EventoProcessado eventoProcessado, CancellationToken cancellationToken);
}
