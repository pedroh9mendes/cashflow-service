using Shared.Contracts;

namespace Consolidado.Domain;

public sealed class SaldoDiario
{
    public Guid ComercianteId { get; private set; }
    public DateOnly Data { get; private set; }
    public decimal TotalCreditos { get; private set; }
    public decimal TotalDebitos { get; private set; }
    public decimal SaldoFinal => TotalCreditos - TotalDebitos;
    public DateTimeOffset AtualizadoEm { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private SaldoDiario() { }

    public static SaldoDiario CriarVazio(Guid comercianteId, DateOnly data)
    {
        return new SaldoDiario
        {
            ComercianteId = comercianteId,
            Data = data,
            TotalCreditos = 0,
            TotalDebitos = 0,
            AtualizadoEm = DateTimeOffset.UtcNow
        };
    }

    public void Aplicar(TipoLancamento tipo, decimal valor)
    {
        if (tipo == TipoLancamento.Credito)
        {
            TotalCreditos += valor;
        }
        else
        {
            TotalDebitos += valor;
        }

        AtualizadoEm = DateTimeOffset.UtcNow;
    }
}
