using Consolidado.Domain;
using FluentAssertions;
using Moq;
using Shared.Contracts;

namespace Consolidado.Tests;

public class AplicarLancamentoServiceTests
{
    private static LancamentoRegistrado CriarEvento(Guid lancamentoId, Guid comercianteId, TipoLancamento tipo, decimal valor) =>
        new(lancamentoId, comercianteId, tipo, valor, new DateOnly(2026, 10, 28), DateTimeOffset.UtcNow, Guid.NewGuid().ToString());

    [Fact]
    public async Task AplicarAsync_EventoNovo_DeveAtualizarSaldoERegistrarComoProcessado()
    {
        var saldoRepositoryMock = new Mock<ISaldoDiarioRepository>();
        var eventoRepositoryMock = new Mock<IEventoProcessadoRepository>();

        var comercianteId = Guid.NewGuid();
        var lancamentoId = Guid.NewGuid();
        var evento = CriarEvento(lancamentoId, comercianteId, TipoLancamento.Credito, 100m);

        eventoRepositoryMock.Setup(r => r.JaProcessadoAsync(lancamentoId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        saldoRepositoryMock
            .Setup(r => r.BuscarAsync(comercianteId, evento.Data, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SaldoDiario?)null);
        saldoRepositoryMock
            .Setup(r => r.TentarSalvarAsync(It.IsAny<SaldoDiario>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new AplicarLancamentoService(saldoRepositoryMock.Object, eventoRepositoryMock.Object);

        var aplicado = await service.AplicarAsync(evento, CancellationToken.None);

        aplicado.Should().BeTrue();
        saldoRepositoryMock.Verify(r => r.TentarSalvarAsync(It.Is<SaldoDiario>(s => s.TotalCreditos == 100m), It.IsAny<CancellationToken>()), Times.Once);
        eventoRepositoryMock.Verify(r => r.RegistrarAsync(It.IsAny<EventoProcessado>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AplicarAsync_EventoJaProcessado_DeveDescartarSemAtualizarSaldo()
    {
        var saldoRepositoryMock = new Mock<ISaldoDiarioRepository>();
        var eventoRepositoryMock = new Mock<IEventoProcessadoRepository>();

        var lancamentoId = Guid.NewGuid();
        var evento = CriarEvento(lancamentoId, Guid.NewGuid(), TipoLancamento.Debito, 30m);

        eventoRepositoryMock.Setup(r => r.JaProcessadoAsync(lancamentoId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = new AplicarLancamentoService(saldoRepositoryMock.Object, eventoRepositoryMock.Object);

        var aplicado = await service.AplicarAsync(evento, CancellationToken.None);

        aplicado.Should().BeFalse();
        saldoRepositoryMock.Verify(r => r.SalvarAsync(It.IsAny<SaldoDiario>(), It.IsAny<CancellationToken>()), Times.Never);
        eventoRepositoryMock.Verify(r => r.RegistrarAsync(It.IsAny<EventoProcessado>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AplicarAsync_MesmoEventoAplicadoDuasVezes_NaoDeveDuplicarEfeitoNoSaldo()
    {
        var saldoRepositoryMock = new Mock<ISaldoDiarioRepository>();
        var eventoRepositoryMock = new Mock<IEventoProcessadoRepository>();

        var comercianteId = Guid.NewGuid();
        var lancamentoId = Guid.NewGuid();
        var evento = CriarEvento(lancamentoId, comercianteId, TipoLancamento.Credito, 100m);

        var processado = false;
        eventoRepositoryMock
            .Setup(r => r.JaProcessadoAsync(lancamentoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => processado);
        eventoRepositoryMock
            .Setup(r => r.RegistrarAsync(It.IsAny<EventoProcessado>(), It.IsAny<CancellationToken>()))
            .Callback(() => processado = true)
            .Returns(Task.CompletedTask);

        saldoRepositoryMock
            .Setup(r => r.BuscarAsync(comercianteId, evento.Data, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SaldoDiario?)null);
        saldoRepositoryMock
            .Setup(r => r.TentarSalvarAsync(It.IsAny<SaldoDiario>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new AplicarLancamentoService(saldoRepositoryMock.Object, eventoRepositoryMock.Object);

        var primeiraAplicacao = await service.AplicarAsync(evento, CancellationToken.None);
        var segundaAplicacao = await service.AplicarAsync(evento, CancellationToken.None);

        primeiraAplicacao.Should().BeTrue();
        segundaAplicacao.Should().BeFalse();
        saldoRepositoryMock.Verify(r => r.TentarSalvarAsync(It.IsAny<SaldoDiario>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AplicarAsync_ConflitoDeConcorrenciaNaPrimeiraTentativa_DeveRelerEReaplicarComSucesso()
    {
        // Simula duas mensagens do mesmo comerciante+data processadas concorrentemente: a
        // primeira tentativa de salvar falha por conflito de concorrência otimista (RowVersion),
        // e o serviço precisa reler o estado mais atual e reaplicar o lançamento antes de
        // conseguir persistir, sem perder o efeito do evento (ver correção de concorrência
        // em AplicarLancamentoService e ISaldoDiarioRepository.TentarSalvarAsync).
        var saldoRepositoryMock = new Mock<ISaldoDiarioRepository>();
        var eventoRepositoryMock = new Mock<IEventoProcessadoRepository>();

        var comercianteId = Guid.NewGuid();
        var lancamentoId = Guid.NewGuid();
        var evento = CriarEvento(lancamentoId, comercianteId, TipoLancamento.Credito, 100m);

        eventoRepositoryMock.Setup(r => r.JaProcessadoAsync(lancamentoId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        saldoRepositoryMock
            .Setup(r => r.BuscarAsync(comercianteId, evento.Data, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SaldoDiario?)null);

        var tentativas = 0;
        saldoRepositoryMock
            .Setup(r => r.TentarSalvarAsync(It.IsAny<SaldoDiario>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                tentativas++;
                return tentativas > 1;
            });

        var service = new AplicarLancamentoService(saldoRepositoryMock.Object, eventoRepositoryMock.Object);

        var aplicado = await service.AplicarAsync(evento, CancellationToken.None);

        aplicado.Should().BeTrue();
        tentativas.Should().Be(2);
        saldoRepositoryMock.Verify(r => r.BuscarAsync(comercianteId, evento.Data, It.IsAny<CancellationToken>()), Times.Exactly(2));
        eventoRepositoryMock.Verify(r => r.RegistrarAsync(It.IsAny<EventoProcessado>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
