using Consolidado.Domain;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure;

public sealed class EventoProcessadoRepository : IEventoProcessadoRepository
{
    private readonly ConsolidadoDbContext _dbContext;

    public EventoProcessadoRepository(ConsolidadoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> JaProcessadoAsync(Guid lancamentoId, CancellationToken cancellationToken)
    {
        return await _dbContext.EventosProcessados.AnyAsync(e => e.LancamentoId == lancamentoId, cancellationToken);
    }

    public async Task RegistrarAsync(EventoProcessado eventoProcessado, CancellationToken cancellationToken)
    {
        await _dbContext.EventosProcessados.AddAsync(eventoProcessado, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
