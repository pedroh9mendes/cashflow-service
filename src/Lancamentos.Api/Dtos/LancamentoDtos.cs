using System.ComponentModel.DataAnnotations;
using Shared.Contracts;

namespace Lancamentos.Api.Dtos;

/// <summary>
/// Dados necessários para registrar um novo lançamento no fluxo de caixa do comerciante autenticado.
/// </summary>
public sealed class CriarLancamentoRequest
{
    /// <summary>Tipo do lançamento: <c>Credito</c> (entrada) ou <c>Debito</c> (saída).</summary>
    [Required]
    public TipoLancamento Tipo { get; set; }

    /// <summary>Valor monetário do lançamento. Deve ser maior que zero.</summary>
    [Range(0.01, 999_999_999.99, ErrorMessage = "O valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    /// <summary>Descrição livre do lançamento (ex: "Venda balcão - pedido 4521").</summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Data de competência do lançamento (não necessariamente a data de hoje).</summary>
    [Required]
    public DateOnly Data { get; set; }
}

/// <summary>Representação de um lançamento já registrado na plataforma.</summary>
public sealed class LancamentoResponse
{
    /// <summary>Identificador único gerado pela plataforma no momento do registro.</summary>
    public Guid Id { get; set; }

    /// <summary>Tipo do lançamento: <c>Credito</c> ou <c>Debito</c>.</summary>
    public TipoLancamento Tipo { get; set; }

    /// <summary>Valor monetário do lançamento.</summary>
    public decimal Valor { get; set; }

    /// <summary>Descrição livre do lançamento.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Data de competência do lançamento.</summary>
    public DateOnly Data { get; set; }

    /// <summary>Data e hora (UTC) em que o lançamento foi efetivamente persistido na plataforma.</summary>
    public DateTimeOffset RegistradoEm { get; set; }
}

/// <summary>Um item do lote de reconciliação, representando um lançamento gerado fora da plataforma.</summary>
public sealed class ReconciliarLancamentoItem
{
    /// <summary>
    /// Identificador único gerado na origem (não pelo servidor) no momento em que o lançamento
    /// foi criado offline. É a chave de idempotência: reenviar o mesmo <c>idOrigem</c> não
    /// gera duplicação.
    /// </summary>
    [Required]
    public Guid IdOrigem { get; set; }

    /// <summary>Tipo do lançamento: <c>Credito</c> ou <c>Debito</c>.</summary>
    [Required]
    public TipoLancamento Tipo { get; set; }

    /// <summary>Valor monetário do lançamento. Deve ser maior que zero.</summary>
    [Range(0.01, 999_999_999.99)]
    public decimal Valor { get; set; }

    /// <summary>Descrição livre do lançamento.</summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Data de competência do lançamento.</summary>
    [Required]
    public DateOnly Data { get; set; }
}

/// <summary>Lote de lançamentos pendentes de sincronização com a plataforma.</summary>
public sealed class ReconciliarLancamentosRequest
{
    /// <summary>Lançamentos a sincronizar. Deve conter ao menos um item.</summary>
    [Required]
    [MinLength(1)]
    public List<ReconciliarLancamentoItem> Lancamentos { get; set; } = new();
}

/// <summary>Resultado do processamento do lote de reconciliação.</summary>
public sealed class ReconciliarLancamentosResponse
{
    /// <summary>Quantidade de lançamentos novos, efetivamente registrados nesta chamada.</summary>
    public int Processados { get; set; }

    /// <summary>
    /// Quantidade de lançamentos ignorados por já terem sido processados anteriormente
    /// (mesmo <c>idOrigem</c>) — não representa erro, é o funcionamento esperado da idempotência.
    /// </summary>
    public int Duplicados { get; set; }
}
