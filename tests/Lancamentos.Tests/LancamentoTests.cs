using FluentAssertions;
using Lancamentos.Domain;
using Shared.Contracts;

namespace Lancamentos.Tests;

public class LancamentoTests
{
    [Fact]
    public void Criar_ComDadosValidos_DeveCriarLancamento()
    {
        var comercianteId = Guid.NewGuid();
        var data = new DateOnly(2026, 10, 28);

        var lancamento = Lancamento.Criar(comercianteId, TipoLancamento.Credito, 150.75m, "Venda balcão", data);

        lancamento.ComercianteId.Should().Be(comercianteId);
        lancamento.Tipo.Should().Be(TipoLancamento.Credito);
        lancamento.Valor.Should().Be(150.75m);
        lancamento.Descricao.Should().Be("Venda balcão");
        lancamento.Data.Should().Be(data);
        lancamento.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Criar_ComIdOrigemInformado_DeveUsarEsseId()
    {
        var idOrigem = Guid.NewGuid();

        var lancamento = Lancamento.Criar(
            Guid.NewGuid(), TipoLancamento.Debito, 10m, "Pagamento fornecedor", DateOnly.FromDateTime(DateTime.UtcNow), idOrigem);

        lancamento.Id.Should().Be(idOrigem);
    }

    [Fact]
    public void Criar_ComComercianteIdVazio_DeveLancarExcecao()
    {
        var acao = () => Lancamento.Criar(Guid.Empty, TipoLancamento.Credito, 10m, "Descrição", DateOnly.FromDateTime(DateTime.UtcNow));

        acao.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Criar_ComValorMenorOuIgualAZero_DeveLancarExcecao(decimal valor)
    {
        var acao = () => Lancamento.Criar(Guid.NewGuid(), TipoLancamento.Credito, valor, "Descrição", DateOnly.FromDateTime(DateTime.UtcNow));

        acao.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComDescricaoVaziaOuEmBranco_DeveLancarExcecao(string descricao)
    {
        var acao = () => Lancamento.Criar(Guid.NewGuid(), TipoLancamento.Credito, 10m, descricao, DateOnly.FromDateTime(DateTime.UtcNow));

        acao.Should().Throw<ArgumentException>();
    }
}
