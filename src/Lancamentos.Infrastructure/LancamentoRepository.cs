using System.Text.Json;
using Lancamentos.Domain;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace Lancamentos.Infrastructure;

public sealed class LancamentoRepository : ILancamentoRepository
{
    private readonly LancamentosDbContext _dbContext;
    private readonly ContingencyCircuitBreaker _circuitBreaker;

    public LancamentoRepository(LancamentosDbContext dbContext, ContingencyCircuitBreaker circuitBreaker)
    {
        _dbContext = dbContext;
        _circuitBreaker = circuitBreaker;
    }

    public async Task AdicionarAsync(Lancamento lancamento, CancellationToken cancellationToken)
    {
        // Lançamento e evento de outbox são gravados na mesma transação local (SaveChanges),
        // garantindo publicação confiável do evento mesmo sob falha do broker (ver ADR-005).
        var evento = new LancamentoRegistrado(
            lancamento.Id,
            lancamento.ComercianteId,
            lancamento.Tipo,
            lancamento.Valor,
            lancamento.Data,
            lancamento.RegistradoEm,
            CorrelationId: Guid.NewGuid().ToString());

        var outboxMessage = OutboxMessage.Criar(
            tipoEvento: nameof(LancamentoRegistrado),
            payload: JsonSerializer.Serialize(evento));

        await _dbContext.Lancamentos.AddAsync(lancamento, cancellationToken);
        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);

        // A gravação no banco primário passa pelo circuit breaker (Camada 2 de resiliência,
        // ver ADR-007/ADR-008): falhas sustentadas de conexão abrem o circuito e ativam o
        // modo contingência, registrado em auditoria e refletido no health check do serviço.
        await _circuitBreaker.Policy.ExecuteAsync(ct => _dbContext.SaveChangesAsync(ct), cancellationToken);

        LancamentosMetrics.LancamentosRegistrados.Inc();
    }

    public async Task<IReadOnlyList<Lancamento>> ListarPorComerciantEDataAsync(
        Guid comercianteId, DateOnly data, CancellationToken cancellationToken)
    {
        return await _dbContext.Lancamentos
            .Where(l => l.ComercianteId == comercianteId && l.Data == data)
            .OrderBy(l => l.RegistradoEm)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExisteAsync(Guid lancamentoId, CancellationToken cancellationToken)
    {
        return await _dbContext.Lancamentos.AnyAsync(l => l.Id == lancamentoId, cancellationToken);
    }
}
