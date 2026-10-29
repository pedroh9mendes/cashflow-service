namespace StatusPage.Api;

public sealed class StatusPageOptions
{
    public const string SectionName = "StatusPage";

    public string LancamentosHealthUrl { get; set; } = "http://localhost:5080/health";
    public string ConsolidadoHealthUrl { get; set; } = "http://localhost:5090/health";
}
