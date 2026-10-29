using Consolidado.Api.Dtos;
using Consolidado.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Security;

namespace Consolidado.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("tenant-elastic")]
[Route("api/consolidado")]
public sealed class ConsolidadoController : ControllerBase
{
    private readonly ISaldoDiarioRepository _repository;

    public ConsolidadoController(ISaldoDiarioRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("saldo-diario")]
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
