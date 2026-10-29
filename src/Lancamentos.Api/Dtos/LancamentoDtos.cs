using System.ComponentModel.DataAnnotations;
using Shared.Contracts;

namespace Lancamentos.Api.Dtos;

public sealed class CriarLancamentoRequest
{
    [Required]
    public TipoLancamento Tipo { get; set; }

    [Range(0.01, 999_999_999.99, ErrorMessage = "O valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Descricao { get; set; } = string.Empty;

    [Required]
    public DateOnly Data { get; set; }
}

public sealed class LancamentoResponse
{
    public Guid Id { get; set; }
    public TipoLancamento Tipo { get; set; }
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateOnly Data { get; set; }
    public DateTimeOffset RegistradoEm { get; set; }
}

public sealed class ReconciliarLancamentoItem
{
    [Required]
    public Guid IdOrigem { get; set; }

    [Required]
    public TipoLancamento Tipo { get; set; }

    [Range(0.01, 999_999_999.99)]
    public decimal Valor { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Descricao { get; set; } = string.Empty;

    [Required]
    public DateOnly Data { get; set; }
}

public sealed class ReconciliarLancamentosRequest
{
    [Required]
    [MinLength(1)]
    public List<ReconciliarLancamentoItem> Lancamentos { get; set; } = new();
}

public sealed class ReconciliarLancamentosResponse
{
    public int Processados { get; set; }
    public int Duplicados { get; set; }
}
