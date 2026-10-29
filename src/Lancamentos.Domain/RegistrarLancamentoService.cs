using Shared.Contracts;

namespace Lancamentos.Domain;

public sealed record RegistrarLancamentoRequest(
    Guid ComercianteId,
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    DateOnly Data,
    Guid? IdOrigem = null);

/// <summary>
/// Caso de uso de registro de lançamento, compartilhado entre o fluxo de API direto e o
/// fluxo de reconciliação da Camada 3 (ver docs/architecture/camada-3-contingencia.md).
/// Quando IdOrigem é informado (reconciliação), a idempotência é garantida verificando
/// se o lançamento já existe antes de gravar novamente.
/// </summary>
public sealed class RegistrarLancamentoService
{
    private readonly ILancamentoRepository _repository;

    public RegistrarLancamentoService(ILancamentoRepository repository)
    {
        _repository = repository;
    }

    public async Task<Lancamento?> RegistrarAsync(RegistrarLancamentoRequest request, CancellationToken cancellationToken)
    {
        if (request.IdOrigem.HasValue && await _repository.ExisteAsync(request.IdOrigem.Value, cancellationToken))
        {
            // Lançamento já processado anteriormente - descarta duplicata silenciosamente,
            // garantindo idempotência na reconciliação da Camada 3 (ADR-007).
            return null;
        }

        var lancamento = Lancamento.Criar(
            request.ComercianteId,
            request.Tipo,
            request.Valor,
            request.Descricao,
            request.Data,
            request.IdOrigem);

        await _repository.AdicionarAsync(lancamento, cancellationToken);

        return lancamento;
    }
}
