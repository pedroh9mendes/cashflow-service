using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StatusPage.Api;

/// <summary>Resumo de volume de processamento e saúde operacional, agregando os dois serviços de negócio.</summary>
public sealed record MetricasAgregadasResponse(
    int LancamentosHoje,
    long LancamentosTotal,
    int ComerciantesAtivos,
    int OutboxPendentes,
    string EstadoCircuitBreaker,
    int ComerciantesComSaldoHoje,
    long ProjecoesTotal,
    long EventosProcessadosTotal,
    double? LatenciaMediaLancamentosMs,
    double? LatenciaMediaConsolidadoMs,
    double? RequisicoesPorSegundo,
    bool Disponivel);

/// <summary>
/// Consulta os endpoints de métricas agregadas de Lançamentos e Consolidado, somados a dados de
/// desempenho (latência média, vazão) direto do Prometheus, e os combina em uma única resposta
/// para os cards de volume de processamento do painel (ver ADR-011 para o raciocínio de manter a
/// status page desacoplada dos serviços que observa). Propositalmente sem nenhum dado financeiro:
/// o objetivo deste painel é dar um panorama de saúde e capacidade de processamento, não de
/// negócio.
/// </summary>
public sealed class MetricasAgregadasService
{
    private readonly HttpClient _httpClient;
    private readonly StatusPageOptions _options;

    public MetricasAgregadasService(HttpClient httpClient, IOptions<StatusPageOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<MetricasAgregadasResponse> ObterAsync(CancellationToken cancellationToken)
    {
        var lancamentos = await ConsultarAsync(_options.LancamentosMetricasUrl, cancellationToken);
        var consolidado = await ConsultarAsync(_options.ConsolidadoMetricasUrl, cancellationToken);

        var disponivel = lancamentos is not null && consolidado is not null;

        var latenciaLancamentos = await ConsultarLatenciaMediaMsAsync("lancamentos-api", cancellationToken);
        var latenciaConsolidado = await ConsultarLatenciaMediaMsAsync("consolidado-api", cancellationToken);
        var requisicoesPorSegundo = await ConsultarRequisicoesPorSegundoAsync(cancellationToken);

        return new MetricasAgregadasResponse(
            LancamentosHoje: lancamentos?.GetProperty("lancamentosHoje").GetInt32() ?? 0,
            LancamentosTotal: lancamentos?.GetProperty("lancamentosTotal").GetInt64() ?? 0,
            ComerciantesAtivos: lancamentos?.GetProperty("comerciantesAtivos").GetInt32() ?? 0,
            OutboxPendentes: lancamentos?.GetProperty("outboxPendentes").GetInt32() ?? 0,
            EstadoCircuitBreaker: lancamentos?.GetProperty("estadoCircuitBreaker").GetString() ?? "Indisponível",
            ComerciantesComSaldoHoje: consolidado?.GetProperty("comerciantesComSaldoHoje").GetInt32() ?? 0,
            ProjecoesTotal: consolidado?.GetProperty("projecoesTotal").GetInt64() ?? 0,
            EventosProcessadosTotal: consolidado?.GetProperty("eventosProcessadosTotal").GetInt64() ?? 0,
            LatenciaMediaLancamentosMs: latenciaLancamentos,
            LatenciaMediaConsolidadoMs: latenciaConsolidado,
            RequisicoesPorSegundo: requisicoesPorSegundo,
            Disponivel: disponivel);
    }

    /// <summary>
    /// Repassa a série diária de volume de processamento (últimos 14 dias) calculada pela
    /// Lançamentos API, usada no gráfico de tendência do painel.
    /// </summary>
    public async Task<JsonElement?> ObterSerieDiariaAsync(CancellationToken cancellationToken)
    {
        return await ConsultarAsync(_options.LancamentosSerieDiariaUrl, cancellationToken);
    }

    private async Task<JsonElement?> ConsultarAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return documento.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private async Task<double?> ConsultarLatenciaMediaMsAsync(string job, CancellationToken cancellationToken)
    {
        var query = $"(sum(rate(http_request_duration_seconds_sum{{job=\"{job}\"}}[5m])) / " +
                     $"sum(rate(http_request_duration_seconds_count{{job=\"{job}\"}}[5m]))) * 1000";

        var valor = await ConsultarPromQLEscalarAsync(query, cancellationToken);
        return valor;
    }

    private async Task<double?> ConsultarRequisicoesPorSegundoAsync(CancellationToken cancellationToken)
    {
        const string query = "sum(rate(http_request_duration_seconds_count[5m]))";
        return await ConsultarPromQLEscalarAsync(query, cancellationToken);
    }

    private async Task<double?> ConsultarPromQLEscalarAsync(string promQl, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{_options.PrometheusUrl}/api/v1/query?query={Uri.EscapeDataString(promQl)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var resultados = documento.RootElement.GetProperty("data").GetProperty("result");
            if (resultados.GetArrayLength() == 0)
            {
                return null;
            }

            var valorTexto = resultados[0].GetProperty("value")[1].GetString();
            if (valorTexto is null || !double.TryParse(valorTexto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor) || double.IsNaN(valor))
            {
                return null;
            }

            return valor;
        }
        catch
        {
            return null;
        }
    }
}
