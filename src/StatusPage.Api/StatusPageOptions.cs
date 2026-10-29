namespace StatusPage.Api;

public sealed class StatusPageOptions
{
    public const string SectionName = "StatusPage";

    public string LancamentosHealthUrl { get; set; } = "http://localhost:5080/health";
    public string ConsolidadoHealthUrl { get; set; } = "http://localhost:5090/health";
    public string LancamentosMetricasUrl { get; set; } = "http://localhost:5080/api/metricas/resumo";
    public string ConsolidadoMetricasUrl { get; set; } = "http://localhost:5090/api/metricas/resumo";
    public string LancamentosSerieDiariaUrl { get; set; } = "http://localhost:5080/api/metricas/serie-diaria";
    public string PrometheusUrl { get; set; } = "http://localhost:9090";
}
