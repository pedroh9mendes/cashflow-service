using Lancamentos.Api.Dtos;
using Lancamentos.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Security;

namespace Lancamentos.Api.Controllers;

/// <summary>
/// Registro e consulta de lançamentos (débitos e créditos) do fluxo de caixa do comerciante
/// autenticado. O comerciante (tenant) nunca é informado pelo cliente da API — é extraído
/// exclusivamente da claim <c>comerciante_id</c> do token JWT, prevenindo acesso cruzado entre
/// tenants (IDOR). Ao persistir um lançamento com sucesso, um evento é publicado de forma
/// confiável (Outbox Pattern) para a fila consumida pelo serviço de Consolidado.
/// </summary>
[ApiController]
[Authorize]
[Route("api/lancamentos")]
[Produces("application/json")]
public sealed class LancamentosController : ControllerBase
{
    private readonly RegistrarLancamentoService _registrarLancamentoService;
    private readonly ILancamentoRepository _repository;

    public LancamentosController(RegistrarLancamentoService registrarLancamentoService, ILancamentoRepository repository)
    {
        _registrarLancamentoService = registrarLancamentoService;
        _repository = repository;
    }

    /// <summary>
    /// Registra um novo lançamento (débito ou crédito) para o comerciante autenticado.
    /// </summary>
    /// <remarks>
    /// Este é o endpoint principal para integração de sistemas externos (incluindo legados)
    /// com a plataforma. A operação é assíncrona no que diz respeito à propagação para o
    /// Consolidado: o lançamento é confirmado de forma transacional e forte aqui, e o saldo
    /// diário é atualizado em segundos via fila de eventos — não espere consistência
    /// imediata ao consultar `GET /api/consolidado/saldo-diario` logo em seguida.
    ///
    /// Exemplo de corpo da requisição:
    ///
    ///     POST /api/lancamentos
    ///     {
    ///        "tipo": "Credito",
    ///        "valor": 1500.00,
    ///        "descricao": "Venda balcão - pedido 4521",
    ///        "data": "2026-10-29"
    ///     }
    /// </remarks>
    /// <param name="request">Dados do lançamento a ser registrado.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">Lançamento registrado com sucesso.</response>
    /// <response code="400">Dados inválidos (valor zero/negativo, descrição vazia, etc).</response>
    /// <response code="401">Token ausente, inválido ou expirado.</response>
    /// <response code="409">Lançamento com o mesmo identificador de origem já foi processado anteriormente (idempotência).</response>
    [HttpPost]
    [ProducesResponseType(typeof(LancamentoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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

    /// <summary>
    /// Lista os lançamentos do comerciante autenticado em uma data específica.
    /// </summary>
    /// <param name="data">Data de referência dos lançamentos, no formato <c>yyyy-MM-dd</c>.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Lista de lançamentos da data informada (vazia se não houver nenhum).</response>
    /// <response code="401">Token ausente, inválido ou expirado.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LancamentoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LancamentoResponse>>> Listar(
        [FromQuery] DateOnly data, CancellationToken cancellationToken)
    {
        var tenant = HttpContext.GetTenantContext();

        var lancamentos = await _repository.ListarPorComerciantEDataAsync(tenant.ComercianteId, data, cancellationToken);

        return Ok(lancamentos.Select(Mapear).ToList());
    }

    /// <summary>
    /// Sincroniza em lote lançamentos gerados fora da plataforma (modo de contingência local
    /// ou integração com sistema legado), de forma idempotente.
    /// </summary>
    /// <remarks>
    /// Este é o endpoint de reconciliação da Camada 3 de resiliência (ver
    /// `docs/architecture/camada-3-contingencia.md`): usado quando um lançamento foi gravado
    /// localmente na origem (ex: ponto do lojista sem conectividade, ou sistema legado que
    /// opera de forma desconectada) e precisa ser sincronizado posteriormente com a plataforma.
    ///
    /// Cada item do lote carrega um <c>idOrigem</c> (GUID gerado na origem, não pelo servidor).
    /// Reenviar o mesmo lote mais de uma vez é seguro: itens cujo <c>idOrigem</c> já foi
    /// processado anteriormente são contados em <c>duplicados</c> e não geram lançamento duplicado.
    ///
    /// Exemplo de corpo da requisição:
    ///
    ///     POST /api/lancamentos/reconciliar
    ///     {
    ///        "lancamentos": [
    ///          {
    ///            "idOrigem": "8f14e45f-ceea-4c5c-8a0d-0d4b2c1f3a11",
    ///            "tipo": "Debito",
    ///            "valor": 320.50,
    ///            "descricao": "Pagamento fornecedor",
    ///            "data": "2026-10-28"
    ///          }
    ///        ]
    ///     }
    /// </remarks>
    /// <param name="request">Lote de lançamentos pendentes de sincronização.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Lote processado (ver contagem de processados/duplicados no corpo da resposta).</response>
    /// <response code="400">Lote vazio ou com itens inválidos.</response>
    /// <response code="401">Token ausente, inválido ou expirado.</response>
    [HttpPost("reconciliar")]
    [ProducesResponseType(typeof(ReconciliarLancamentosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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
