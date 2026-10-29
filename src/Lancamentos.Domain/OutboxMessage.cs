namespace Lancamentos.Domain;

/// <summary>
/// Representa um evento pendente de publicação no broker, gravado na mesma transação
/// do lançamento de origem para garantir publicação confiável (Outbox Pattern, ver ADR-005).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? PublicadoEm { get; private set; }
    public int TentativasPublicacao { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Criar(string tipoEvento, string payload)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TipoEvento = tipoEvento,
            Payload = payload,
            CriadoEm = DateTimeOffset.UtcNow,
            TentativasPublicacao = 0
        };
    }

    public void MarcarComoPublicado()
    {
        PublicadoEm = DateTimeOffset.UtcNow;
    }

    public void RegistrarTentativaFalha()
    {
        TentativasPublicacao++;
    }
}
