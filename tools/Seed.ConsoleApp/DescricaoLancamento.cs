namespace Seed.ConsoleApp;

public static class DescricaoLancamento
{
    public static readonly (string Descricao, decimal Min, decimal Max)[] Creditos =
    {
        ("Venda balcao", 15m, 350m),
        ("Recebimento cartao de credito", 40m, 900m),
        ("Recebimento cartao de debito", 20m, 500m),
        ("Recebimento via Pix", 10m, 700m),
        ("Venda por encomenda", 80m, 1200m),
        ("Recebimento de servico prestado", 50m, 600m),
    };

    public static readonly (string Descricao, decimal Min, decimal Max)[] Debitos =
    {
        ("Pagamento a fornecedor", 100m, 2500m),
        ("Despesa operacional", 30m, 400m),
        ("Taxa de servico bancario", 5m, 60m),
        ("Pagamento de conta de energia", 80m, 600m),
        ("Compra de insumos", 50m, 1800m),
        ("Pagamento de frete", 20m, 300m),
        ("Manutencao de equipamento", 60m, 900m),
    };
}
