using Shared.Contracts;

namespace Consolidado.Domain;

/// <summary>
/// Aplica um evento de lançamento à projeção de saldo diário (read model do CQRS, ver ADR-004),
/// garantindo idempotência: o mesmo LancamentoId nunca é aplicado duas vezes.
/// </summary>
public sealed class AplicarLancamentoService
{
    private readonly ISaldoDiarioRepository _saldoDiarioRepository;
    private readonly IEventoProcessadoRepository _eventoProcessadoRepository;

    public AplicarLancamentoService(
        ISaldoDiarioRepository saldoDiarioRepository,
        IEventoProcessadoRepository eventoProcessadoRepository)
    {
        _saldoDiarioRepository = saldoDiarioRepository;
        _eventoProcessadoRepository = eventoProcessadoRepository;
    }

    private const int MaxTentativasConcorrencia = 5;

    public async Task<bool> AplicarAsync(LancamentoRegistrado evento, CancellationToken cancellationToken)
    {
        if (await _eventoProcessadoRepository.JaProcessadoAsync(evento.LancamentoId, cancellationToken))
        {
            return false;
        }

        // Duas mensagens do mesmo tenant+data podem ser processadas concorrentemente
        // (prefetch > 1 no consumidor). A projeção usa um token de concorrência otimista
        // (RowVersion); em caso de conflito, a leitura e a aplicação são refeitas a partir
        // do estado mais recente, para que nenhum lançamento seja perdido por sobrescrita.
        for (var tentativa = 1; tentativa <= MaxTentativasConcorrencia; tentativa++)
        {
            var saldoDiario = await _saldoDiarioRepository.BuscarAsync(evento.ComercianteId, evento.Data, cancellationToken)
                ?? SaldoDiario.CriarVazio(evento.ComercianteId, evento.Data);

            saldoDiario.Aplicar(evento.Tipo, evento.Valor);

            var sucesso = await _saldoDiarioRepository.TentarSalvarAsync(saldoDiario, cancellationToken);

            if (sucesso)
            {
                await _eventoProcessadoRepository.RegistrarAsync(EventoProcessado.Criar(evento.LancamentoId), cancellationToken);
                return true;
            }
        }

        throw new InvalidOperationException(
            $"Não foi possível atualizar o saldo diário do comerciante {evento.ComercianteId} na data {evento.Data} após {MaxTentativasConcorrencia} tentativas por conflito de concorrência.");
    }
}
