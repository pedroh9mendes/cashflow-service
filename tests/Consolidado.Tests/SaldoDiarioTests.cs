using Consolidado.Domain;
using FluentAssertions;
using Shared.Contracts;

namespace Consolidado.Tests;

public class SaldoDiarioTests
{
    [Fact]
    public void CriarVazio_DeveIniciarComSaldoZerado()
    {
        var saldo = SaldoDiario.CriarVazio(Guid.NewGuid(), new DateOnly(2026, 10, 28));

        saldo.TotalCreditos.Should().Be(0);
        saldo.TotalDebitos.Should().Be(0);
        saldo.SaldoFinal.Should().Be(0);
    }

    [Fact]
    public void Aplicar_Credito_DeveIncrementarTotalCreditosEAumentarSaldo()
    {
        var saldo = SaldoDiario.CriarVazio(Guid.NewGuid(), new DateOnly(2026, 10, 28));

        saldo.Aplicar(TipoLancamento.Credito, 100m);

        saldo.TotalCreditos.Should().Be(100m);
        saldo.TotalDebitos.Should().Be(0);
        saldo.SaldoFinal.Should().Be(100m);
    }

    [Fact]
    public void Aplicar_Debito_DeveIncrementarTotalDebitosEDiminuirSaldo()
    {
        var saldo = SaldoDiario.CriarVazio(Guid.NewGuid(), new DateOnly(2026, 10, 28));
        saldo.Aplicar(TipoLancamento.Credito, 200m);

        saldo.Aplicar(TipoLancamento.Debito, 50m);

        saldo.TotalCreditos.Should().Be(200m);
        saldo.TotalDebitos.Should().Be(50m);
        saldo.SaldoFinal.Should().Be(150m);
    }

    [Fact]
    public void Aplicar_MultiplosLancamentos_DeveAcumularCorretamente()
    {
        var saldo = SaldoDiario.CriarVazio(Guid.NewGuid(), new DateOnly(2026, 10, 28));

        saldo.Aplicar(TipoLancamento.Credito, 300m);
        saldo.Aplicar(TipoLancamento.Credito, 150m);
        saldo.Aplicar(TipoLancamento.Debito, 80m);
        saldo.Aplicar(TipoLancamento.Debito, 20m);

        saldo.TotalCreditos.Should().Be(450m);
        saldo.TotalDebitos.Should().Be(100m);
        saldo.SaldoFinal.Should().Be(350m);
    }
}
