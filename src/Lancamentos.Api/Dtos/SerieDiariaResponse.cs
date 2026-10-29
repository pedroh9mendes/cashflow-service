namespace Lancamentos.Api.Dtos;

/// <summary>Volume de lançamentos (contagem, não valor) registrados em um dia específico.</summary>
public sealed class PontoSerieDiaria
{
    /// <summary>Data de referência.</summary>
    public DateOnly Data { get; set; }

    /// <summary>Quantidade de lançamentos do tipo Crédito registrados nesta data.</summary>
    public int Creditos { get; set; }

    /// <summary>Quantidade de lançamentos do tipo Débito registrados nesta data.</summary>
    public int Debitos { get; set; }
}

/// <summary>
/// Série histórica de volume de processamento, usada para o gráfico de tendência do painel de
/// observabilidade. Expõe apenas contagens (nunca valores monetários).
/// </summary>
public sealed class SerieDiariaResponse
{
    /// <summary>Pontos da série, um por dia, em ordem cronológica.</summary>
    public List<PontoSerieDiaria> Pontos { get; set; } = new();
}
