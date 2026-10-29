namespace StatusPage.Api;

/// <summary>
/// Credenciais do painel de operação. A senha é armazenada apenas como hash SHA-256 na
/// configuração (nunca em texto puro) — ver <see cref="DashboardAuthService"/> para a verificação.
/// Valores padrão existem para não quebrar o ambiente de demonstração local, mas devem sempre ser
/// sobrepostos via variável de ambiente em qualquer implantação real.
/// </summary>
public sealed class DashboardAuthOptions
{
    public const string SectionName = "DashboardAuth";

    public string Usuario { get; set; } = "admin";

    /// <summary>Hash SHA-256 (hex, minúsculo) da senha padrão "admin123".</summary>
    public string SenhaHash { get; set; } = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9";
}
