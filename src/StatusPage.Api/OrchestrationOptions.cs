namespace StatusPage.Api;

public sealed class OrchestrationOptions
{
    public const string SectionName = "Orchestration";

    /// <summary>
    /// Trava explícita de ambiente: quando true, os endpoints de orquestração (scripts SQL e
    /// seed de demonstração) são bloqueados incondicionalmente, independente do estado dos
    /// serviços de negócio. Deve ser definida via configuração (ex: variável de ambiente
    /// Orchestration__AmbienteProducao=true) ao implantar em um ambiente real — nunca inferida.
    /// </summary>
    public bool AmbienteProducao { get; set; } = false;

    public string SqlServerConnectionString { get; set; } =
        "Server=sqlserver,1433;User Id=sa;Password=Fluxo@Caixa2026!;TrustServerCertificate=True;";

    public string SeedDllPath { get; set; } = "/app/seed/Seed.ConsoleApp.dll";

    public string SeedLancamentosBaseUrl { get; set; } = "http://lancamentos-api:8080";

    public string SeedConsolidadoBaseUrl { get; set; } = "http://consolidado-api:8080";

    public string[] SqlScriptsOrder { get; set; } =
    {
        "001_lancamentos_create_tables.sql",
        "002_consolidado_create_tables.sql",
        "003_lancamentos_row_level_security.sql",
        "004_consolidado_row_level_security.sql"
    };

    public string SqlScriptsPath { get; set; } = "/app/database-scripts";
}
