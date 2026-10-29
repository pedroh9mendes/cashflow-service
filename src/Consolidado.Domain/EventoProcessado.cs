namespace Consolidado.Domain;

/// <summary>
/// Registro de controle de idempotência: um LancamentoId só pode ser aplicado uma vez
/// à projeção de saldo, mesmo que o evento correspondente seja entregue mais de uma vez
/// pelo broker (entrega at-least-once, ver ADR-005).
/// </summary>
public sealed class EventoProcessado
{
    public Guid LancamentoId { get; private set; }
    public DateTimeOffset ProcessadoEm { get; private set; }

    private EventoProcessado() { }

    public static EventoProcessado Criar(Guid lancamentoId)
    {
        return new EventoProcessado
        {
            LancamentoId = lancamentoId,
            ProcessadoEm = DateTimeOffset.UtcNow
        };
    }
}
