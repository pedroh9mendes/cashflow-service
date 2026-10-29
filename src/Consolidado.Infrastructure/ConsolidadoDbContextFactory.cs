using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Consolidado.Infrastructure;

/// <summary>
/// Usada apenas pelas ferramentas de design-time do EF Core (dotnet ef migrations)
/// para criar um DbContext fora do pipeline normal de injeção de dependência da API.
/// </summary>
public sealed class ConsolidadoDbContextFactory : IDesignTimeDbContextFactory<ConsolidadoDbContext>
{
    public ConsolidadoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConsolidadoDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost,1433;Database=ConsolidadoDb;User Id=sa;Password=Fluxo@Caixa2026!;TrustServerCertificate=True;");

        return new ConsolidadoDbContext(optionsBuilder.Options);
    }
}
