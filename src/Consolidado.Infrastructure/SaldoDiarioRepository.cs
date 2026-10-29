using Consolidado.Domain;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure;

public sealed class SaldoDiarioRepository : ISaldoDiarioRepository
{
    private readonly ConsolidadoDbContext _dbContext;

    public SaldoDiarioRepository(ConsolidadoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SaldoDiario?> BuscarAsync(Guid comercianteId, DateOnly data, CancellationToken cancellationToken)
    {
        return await _dbContext.SaldosDiarios
            .FirstOrDefaultAsync(s => s.ComercianteId == comercianteId && s.Data == data, cancellationToken);
    }

    public async Task SalvarAsync(SaldoDiario saldoDiario, CancellationToken cancellationToken)
    {
        await TentarSalvarAsync(saldoDiario, cancellationToken);
    }

    public async Task<bool> TentarSalvarAsync(SaldoDiario saldoDiario, CancellationToken cancellationToken)
    {
        var existente = await _dbContext.SaldosDiarios
            .FirstOrDefaultAsync(s => s.ComercianteId == saldoDiario.ComercianteId && s.Data == saldoDiario.Data, cancellationToken);

        if (existente is null)
        {
            await _dbContext.SaldosDiarios.AddAsync(saldoDiario, cancellationToken);
        }
        else
        {
            _dbContext.Entry(existente).CurrentValues.SetValues(saldoDiario);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.Entry(existente ?? saldoDiario).State = EntityState.Detached;
            return false;
        }
    }
}
