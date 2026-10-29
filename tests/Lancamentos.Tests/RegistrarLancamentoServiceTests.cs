using FluentAssertions;
using Lancamentos.Domain;
using Moq;
using Shared.Contracts;

namespace Lancamentos.Tests;

public class RegistrarLancamentoServiceTests
{
    [Fact]
    public async Task RegistrarAsync_SemIdOrigem_DeveRegistrarNormalmente()
    {
        var repositoryMock = new Mock<ILancamentoRepository>();
        var service = new RegistrarLancamentoService(repositoryMock.Object);

        var request = new RegistrarLancamentoRequest(
            Guid.NewGuid(), TipoLancamento.Credito, 100m, "Venda", DateOnly.FromDateTime(DateTime.UtcNow));

        var resultado = await service.RegistrarAsync(request, CancellationToken.None);

        resultado.Should().NotBeNull();
        repositoryMock.Verify(r => r.AdicionarAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarAsync_ComIdOrigemJaExistente_DeveDescartarSemGravarNovamente()
    {
        var idOrigem = Guid.NewGuid();
        var repositoryMock = new Mock<ILancamentoRepository>();
        repositoryMock.Setup(r => r.ExisteAsync(idOrigem, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = new RegistrarLancamentoService(repositoryMock.Object);

        var request = new RegistrarLancamentoRequest(
            Guid.NewGuid(), TipoLancamento.Debito, 50m, "Reconciliação", DateOnly.FromDateTime(DateTime.UtcNow), idOrigem);

        var resultado = await service.RegistrarAsync(request, CancellationToken.None);

        resultado.Should().BeNull();
        repositoryMock.Verify(r => r.AdicionarAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAsync_ComIdOrigemInexistente_DeveRegistrarComOMesmoId()
    {
        var idOrigem = Guid.NewGuid();
        var repositoryMock = new Mock<ILancamentoRepository>();
        repositoryMock.Setup(r => r.ExisteAsync(idOrigem, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Lancamento? lancamentoCapturado = null;
        repositoryMock
            .Setup(r => r.AdicionarAsync(It.IsAny<Lancamento>(), It.IsAny<CancellationToken>()))
            .Callback<Lancamento, CancellationToken>((l, _) => lancamentoCapturado = l)
            .Returns(Task.CompletedTask);

        var service = new RegistrarLancamentoService(repositoryMock.Object);

        var request = new RegistrarLancamentoRequest(
            Guid.NewGuid(), TipoLancamento.Credito, 75m, "Reconciliação", DateOnly.FromDateTime(DateTime.UtcNow), idOrigem);

        var resultado = await service.RegistrarAsync(request, CancellationToken.None);

        resultado.Should().NotBeNull();
        lancamentoCapturado.Should().NotBeNull();
        lancamentoCapturado!.Id.Should().Be(idOrigem);
    }
}
