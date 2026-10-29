using Consolidado.Api.Dtos;
using Consolidado.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Security;

namespace Consolidado.Api.Controllers;

/// <summary>
/// Consulta do saldo diário consolidado do comerciante autenticado — uma projeção de leitura
/// (CQRS) mantida assincronamente a partir dos eventos publicados pela Lançamentos API. Somente
/// leitura: este serviço nunca aceita registro de lançamentos.
/// </summary>
[ApiController]
[Authorize]
[EnableRateLimiting("tenant-elastic")]
[Route("api/consolidado")]
[Produces("application/json")]
public sealed class ConsolidadoController : ControllerBase
{
    private readonly ISaldoDiarioRepository _repository;

    public ConsolidadoController(ISaldoDiarioRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Retorna o saldo consolidado (créditos, débitos e saldo final) do comerciante autenticado
    /// em uma data específica.
    /// </summary>
    /// <remarks>
    /// A projeção é atualizada de forma assíncrona conforme os eventos de lançamento chegam pela
    /// fila — pode haver um atraso de poucos segundos entre o registro de um lançamento na
    /// Lançamentos API e sua reflexão aqui (consistência eventual). Se nenhum lançamento foi
    /// registrado na data informada, a resposta retorna com todos os totais zerados, não um 404.
    ///
    /// Este endpoint possui limite de requisições por tenant (rate limiting elástico): em caso de
    /// uso acima da cota, retorna `429 Too Many Requests`.
    /// </remarks>
    /// <param name="data">Data de referência do saldo consolidado, no formato <c>yyyy-MM-dd</c>.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Saldo consolidado da data informada.</response>
    /// <response code="401">Token ausente, inválido ou expirado.</response>
    /// <response code="429">Limite de requisições do tenant excedido.</response>
    [HttpGet("saldo-diario")]
    [ProducesResponseType(typeof(SaldoDiarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SaldoDiarioResponse>> ObterSaldoDiario(
        [FromQuery] DateOnly data, CancellationToken cancellationToken)
    {
        var tenant = HttpContext.GetTenantContext();

        var saldoDiario = await _repository.BuscarAsync(tenant.ComercianteId, data, cancellationToken)
            ?? SaldoDiario.CriarVazio(tenant.ComercianteId, data);

        return Ok(new SaldoDiarioResponse
        {
            Data = saldoDiario.Data,
            TotalCreditos = saldoDiario.TotalCreditos,
            TotalDebitos = saldoDiario.TotalDebitos,
            SaldoFinal = saldoDiario.SaldoFinal,
            AtualizadoEm = saldoDiario.AtualizadoEm
        });
    }
}
