using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure;

/// <summary>
/// Aplica migrations pendentes e o script de Row-Level Security na inicialização do serviço,
/// eliminando a necessidade de um passo manual separado ao subir o ambiente via docker-compose.
/// RLS não é modelado nativamente pelo EF Core, por isso é aplicado como script SQL bruto,
/// dividido pelos marcadores GO (que não são uma instrução T-SQL válida, apenas um separador
/// de lote reconhecido por ferramentas como o sqlcmd, não pelo ADO.NET).
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InicializarAsync(ConsolidadoDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await AplicarScriptSqlAsync(dbContext, "Consolidado.Infrastructure.Sql.001_row_level_security.sql", cancellationToken);
    }

    private static async Task AplicarScriptSqlAsync(ConsolidadoDbContext dbContext, string recursoEmbarcado, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();

        await using var stream = assembly.GetManifestResourceStream(recursoEmbarcado)
            ?? throw new InvalidOperationException($"Recurso embarcado '{recursoEmbarcado}' não encontrado.");

        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync(cancellationToken);

        var lotes = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(lote => lote.Trim())
            .Where(lote => !string.IsNullOrWhiteSpace(lote));

        foreach (var lote in lotes)
        {
            await dbContext.Database.ExecuteSqlRawAsync(lote, cancellationToken);
        }
    }
}
