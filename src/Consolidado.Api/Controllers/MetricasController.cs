using Consolidado.Api.Dtos;
using Consolidado.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Api.Controllers;

/// <summary>
/// Métricas operacionais agregadas do serviço de Consolidado, consumidas pelo painel interno de
/// observabilidade (não pelo lojista). Intencionalmente sem autenticação de tenant: os dados
/// expostos aqui são contagens agregadas entre todos os comerciantes, nunca um recorte individual
/// de um tenant específico — não há superfície de IDOR a proteger neste endpoint.
/// </summary>
[ApiController]
[Route("api/metricas")]
[Produces("application/json")]
public sealed class MetricasController : ControllerBase
{
    private readonly ConsolidadoDbContext _dbContext;

    public MetricasController(ConsolidadoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retorna um resumo agregado de volume de dados do serviço de Consolidado, usado para
    /// alimentar os cards de métricas do painel de observabilidade.
    /// </summary>
    /// <response code="200">Resumo agregado calculado com sucesso.</response>
    [HttpGet("resumo")]
    [ProducesResponseType(typeof(MetricasResumoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MetricasResumoResponse>> ObterResumo(CancellationToken cancellationToken)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var comerciantesComSaldoHoje = await _dbContext.SaldosDiarios
            .CountAsync(s => s.Data == hoje, cancellationToken);

        var projecoesTotal = await _dbContext.SaldosDiarios.LongCountAsync(cancellationToken);
        var eventosProcessadosTotal = await _dbContext.EventosProcessados.LongCountAsync(cancellationToken);

        return Ok(new MetricasResumoResponse
        {
            ComerciantesComSaldoHoje = comerciantesComSaldoHoje,
            ProjecoesTotal = projecoesTotal,
            EventosProcessadosTotal = eventosProcessadosTotal
        });
    }
}
