using Microsoft.Extensions.Options;

namespace StatusPage.Api;

public enum StatusServico
{
    Operacional,
    Degradado,
    Indisponivel
}

public sealed record StatusServicoInfo(string Servico, StatusServico Status);

/// <summary>
/// Agrega o /health de Lançamentos e Consolidado em um status consolidado, consumido pela
/// status page pública de autoatendimento do lojista (ver ADR-011).
///
/// Roda como serviço independente, não acoplado a nenhum dos dois serviços monitorados:
/// se o Consolidado cair, a página que deveria reportar essa indisponibilidade continua
/// no ar (ela própria depende apenas da sua própria infraestrutura, nunca da saúde dos
/// serviços que observa). Expõe apenas estado agregado de alto nível, sem detalhes de
/// infraestrutura subjacente, conforme docs/adr/ADR-011-status-page-publica.md.
/// </summary>
public sealed class StatusPageService
{
    private readonly HttpClient _httpClient;
    private readonly StatusPageOptions _options;

    public StatusPageService(HttpClient httpClient, IOptions<StatusPageOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<StatusServicoInfo>> ObterStatusConsolidadoAsync(CancellationToken cancellationToken)
    {
        var lancamentos = await ConsultarAsync("Lancamentos", _options.LancamentosHealthUrl, cancellationToken);
        var consolidado = await ConsultarAsync("Consolidado", _options.ConsolidadoHealthUrl, cancellationToken);

        return new[] { lancamentos, consolidado };
    }

    private async Task<StatusServicoInfo> ConsultarAsync(string nomeServico, string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            var status = response.StatusCode switch
            {
                System.Net.HttpStatusCode.OK => StatusServico.Operacional,
                System.Net.HttpStatusCode.ServiceUnavailable => StatusServico.Indisponivel,
                _ => StatusServico.Degradado
            };

            return new StatusServicoInfo(nomeServico, status);
        }
        catch
        {
            return new StatusServicoInfo(nomeServico, StatusServico.Indisponivel);
        }
    }
}
