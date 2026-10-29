namespace Seed.ConsoleApp;

public enum PorteComerciante
{
    Pequeno,
    Medio,
    Grande
}

public sealed record Comerciante(Guid Id, string Nome, PorteComerciante Porte)
{
    public (int Min, int Max) FaixaLancamentosPorDia => Porte switch
    {
        PorteComerciante.Pequeno => (1, 4),
        PorteComerciante.Medio => (5, 12),
        PorteComerciante.Grande => (15, 30),
        _ => throw new ArgumentOutOfRangeException()
    };
}

public static class CarteiraComerciantes
{
    // GUIDs fixos para reprodutibilidade entre execuções do seed.
    public static readonly IReadOnlyList<Comerciante> Todos = new List<Comerciante>
    {
        new(Guid.Parse("a0000001-0000-0000-0000-000000000001"), "Mercado Bom Preco", PorteComerciante.Medio),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000002"), "Farmacia Vida Saudavel", PorteComerciante.Medio),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000003"), "Padaria Pao Dourado", PorteComerciante.Pequeno),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000004"), "Loja de Roupas Estilo Urbano", PorteComerciante.Pequeno),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000005"), "Supermercado Reis", PorteComerciante.Grande),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000006"), "Papelaria Criativa", PorteComerciante.Pequeno),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000007"), "Pet Shop Amigo Fiel", PorteComerciante.Pequeno),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000008"), "Oficina Mecanica Veloz", PorteComerciante.Medio),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000009"), "Restaurante Sabor Caseiro", PorteComerciante.Medio),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000010"), "Rede de Farmacias Saude Plena", PorteComerciante.Grande),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000011"), "Lanchonete do Zeca", PorteComerciante.Pequeno),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000012"), "Loja de Materiais de Construcao Forte", PorteComerciante.Medio),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000013"), "Salao de Beleza Charme", PorteComerciante.Pequeno),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000014"), "Distribuidora de Bebidas Fresh", PorteComerciante.Grande),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000015"), "Livraria Pagina Virada", PorteComerciante.Pequeno),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000016"), "Academia Corpo em Forma", PorteComerciante.Medio),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000017"), "Floricultura Jardim Encantado", PorteComerciante.Pequeno),
    };
}
