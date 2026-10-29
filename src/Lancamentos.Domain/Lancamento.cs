using Shared.Contracts;

namespace Lancamentos.Domain;

public sealed class Lancamento
{
    public Guid Id { get; private set; }
    public Guid ComercianteId { get; private set; }
    public TipoLancamento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public DateOnly Data { get; private set; }
    public DateTimeOffset RegistradoEm { get; private set; }

    private Lancamento() { }

    public static Lancamento Criar(
        Guid comercianteId,
        TipoLancamento tipo,
        decimal valor,
        string descricao,
        DateOnly data,
        Guid? idOrigem = null)
    {
        if (comercianteId == Guid.Empty)
        {
            throw new ArgumentException("ComercianteId é obrigatório.", nameof(comercianteId));
        }

        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "O valor do lançamento deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("A descrição do lançamento é obrigatória.", nameof(descricao));
        }

        return new Lancamento
        {
            Id = idOrigem ?? Guid.NewGuid(),
            ComercianteId = comercianteId,
            Tipo = tipo,
            Valor = valor,
            Descricao = descricao.Trim(),
            Data = data,
            RegistradoEm = DateTimeOffset.UtcNow
        };
    }
}
