using Lancamentos.Api.Dtos;
using Lancamentos.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Security;

namespace Lancamentos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lancamentos")]
public sealed class LancamentosController : ControllerBase
{
    private readonly RegistrarLancamentoService _registrarLancamentoService;
    private readonly ILancamentoRepository _repository;

    public LancamentosController(RegistrarLancamentoService registrarLancamentoService, ILancamentoRepository repository)
    {
        _registrarLancamentoService = registrarLancamentoService;
        _repository = repository;
    }

    [HttpPost]
    public async Task<ActionResult<LancamentoResponse>> Criar(
        [FromBody] CriarLancamentoRequest request, CancellationToken cancellationToken)
    {
        var tenant = HttpContext.GetTenantContext();

        var lancamento = await _registrarLancamentoService.RegistrarAsync(
            new RegistrarLancamentoRequest(tenant.ComercianteId, request.Tipo, request.Valor, request.Descricao, request.Data),
            cancellationToken);

        if (lancamento is null)
        {
            return Conflict();
        }

        return CreatedAtAction(nameof(Criar), new { id = lancamento.Id }, Mapear(lancamento));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LancamentoResponse>>> Listar(
        [FromQuery] DateOnly data, CancellationToken cancellationToken)
    {
        var tenant = HttpContext.GetTenantContext();

        var lancamentos = await _repository.ListarPorComerciantEDataAsync(tenant.ComercianteId, data, cancellationToken);

        return Ok(lancamentos.Select(Mapear).ToList());
    }

    [HttpPost("reconciliar")]
    public async Task<ActionResult<ReconciliarLancamentosResponse>> Reconciliar(
        [FromBody] ReconciliarLancamentosRequest request, CancellationToken cancellationToken)
    {
        var tenant = HttpContext.GetTenantContext();
        var response = new ReconciliarLancamentosResponse();

        foreach (var item in request.Lancamentos)
        {
            var resultado = await _registrarLancamentoService.RegistrarAsync(
                new RegistrarLancamentoRequest(tenant.ComercianteId, item.Tipo, item.Valor, item.Descricao, item.Data, item.IdOrigem),
                cancellationToken);

            if (resultado is null)
            {
                response.Duplicados++;
            }
            else
            {
                response.Processados++;
            }
        }

        return Ok(response);
    }

    private static LancamentoResponse Mapear(Lancamento lancamento) => new()
    {
        Id = lancamento.Id,
        Tipo = lancamento.Tipo,
        Valor = lancamento.Valor,
        Descricao = lancamento.Descricao,
        Data = lancamento.Data,
        RegistradoEm = lancamento.RegistradoEm
    };
}
