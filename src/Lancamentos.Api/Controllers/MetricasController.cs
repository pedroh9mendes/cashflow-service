using Lancamentos.Api.Dtos;
using Lancamentos.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lancamentos.Api.Controllers;

/// <summary>
/// Métricas operacionais agregadas do serviço de Lançamentos, consumidas pelo painel interno de
/// observabilidade (não pelo lojista). Intencionalmente sem autenticação de tenant: os dados
/// expostos aqui são contagens agregadas entre todos os comerciantes, nunca um recorte individual
/// de um tenant específico — não há superfície de IDOR a proteger neste endpoint.
/// </summary>
[ApiController]
[Route("api/metricas")]
[Produces("application/json")]
public sealed class MetricasController : ControllerBase
{
    private readonly LancamentosDbContext _dbContext;
    private readonly ContingencyCircuitBreaker _circuitBreaker;

    public MetricasController(LancamentosDbContext dbContext, ContingencyCircuitBreaker circuitBreaker)
    {
        _dbContext = dbContext;
        _circuitBreaker = circuitBreaker;
    }

    /// <summary>
    /// Retorna um resumo agregado de volume de dados e saúde operacional do serviço de
    /// Lançamentos, usado para alimentar os cards de métricas do painel de observabilidade.
    /// </summary>
    /// <response code="200">Resumo agregado calculado com sucesso.</response>
    [HttpGet("resumo")]
    [ProducesResponseType(typeof(MetricasResumoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MetricasResumoResponse>> ObterResumo(CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var lancamentosHoje = await _dbContext.Lancamentos
            .CountAsync(l => l.Data == hoje, cancellationToken);

        var lancamentosTotal = await _dbContext.Lancamentos.LongCountAsync(cancellationToken);
        var comerciantesAtivos = await _dbContext.Lancamentos
            .Select(l => l.ComercianteId)
            .Distinct()
            .CountAsync(cancellationToken);

        var outboxPendentes = await _dbContext.OutboxMessages
            .CountAsync(o => o.PublicadoEm == null, cancellationToken);

        return Ok(new MetricasResumoResponse
        {
            LancamentosHoje = lancamentosHoje,
            LancamentosTotal = lancamentosTotal,
            ComerciantesAtivos = comerciantesAtivos,
            OutboxPendentes = outboxPendentes,
            EstadoCircuitBreaker = _circuitBreaker.ModoAtual.ToString()
        });
    }

    /// <summary>
    /// Retorna o volume de lançamentos (contagem, por tipo) dos últimos 14 dias, usado para o
    /// gráfico de tendência de processamento do painel de observabilidade.
    /// </summary>
    /// <response code="200">Série diária calculada com sucesso.</response>
    [HttpGet("serie-diaria")]
    [ProducesResponseType(typeof(SerieDiariaResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SerieDiariaResponse>> ObterSerieDiaria(CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var inicio = hoje.AddDays(-13);

        var lancamentos = await _dbContext.Lancamentos
            .Where(l => l.Data >= inicio && l.Data <= hoje)
            .Select(l => new { l.Data, l.Tipo })
            .ToListAsync(cancellationToken);

        var pontos = Enumerable.Range(0, 14)
            .Select(offset => inicio.AddDays(offset))
            .Select(data => new PontoSerieDiaria
            {
                Data = data,
                Creditos = lancamentos.Count(l => l.Data == data && l.Tipo == Shared.Contracts.TipoLancamento.Credito),
                Debitos = lancamentos.Count(l => l.Data == data && l.Tipo == Shared.Contracts.TipoLancamento.Debito)
            })
            .ToList();

        return Ok(new SerieDiariaResponse { Pontos = pontos });
    }
}
