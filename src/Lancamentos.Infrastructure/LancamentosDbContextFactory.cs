using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lancamentos.Infrastructure;

/// <summary>
/// Usada apenas pelas ferramentas de design-time do EF Core (dotnet ef migrations)
/// para criar um DbContext fora do pipeline normal de injeção de dependência da API.
/// </summary>
public sealed class LancamentosDbContextFactory : IDesignTimeDbContextFactory<LancamentosDbContext>
{
    public LancamentosDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LancamentosDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost,1433;Database=LancamentosDb;User Id=sa;Password=Fluxo@Caixa2026!;TrustServerCertificate=True;");

        return new LancamentosDbContext(optionsBuilder.Options);
    }
}
