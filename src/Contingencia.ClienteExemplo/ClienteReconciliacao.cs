using System.Net.Http.Json;

namespace Contingencia.ClienteExemplo;

public sealed class ClienteReconciliacao
{
    private readonly HttpClient _httpClient;

    public ClienteReconciliacao(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> SincronizarAsync(IReadOnlyList<RegistroLocal> pendentes, CancellationToken cancellationToken)
    {
        if (pendentes.Count == 0)
        {
            return true;
        }

        var payload = new
        {
            Lancamentos = pendentes.Select(p => new
            {
                IdOrigem = p.IdOrigem,
                Tipo = p.Tipo,
                Valor = p.Valor,
                Descricao = p.Descricao,
                Data = p.Data
            })
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/lancamentos/reconciliar", payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}
