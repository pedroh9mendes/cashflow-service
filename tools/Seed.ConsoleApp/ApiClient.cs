using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Seed.ConsoleApp;

public sealed record EmitirTokenRequest(Guid ComercianteId);
public sealed record EmitirTokenResponse(string Token, DateTimeOffset ExpiraEm);

public enum TipoLancamento
{
    Credito = 1,
    Debito = 2
}

public sealed record CriarLancamentoRequest(
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    DateOnly Data);

public sealed record SaldoDiarioResponse(
    DateOnly Data,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoFinal,
    DateTimeOffset AtualizadoEm);

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _lancamentosClient;
    private readonly HttpClient _consolidadoClient;

    public ApiClient(string lancamentosBaseUrl, string consolidadoBaseUrl)
    {
        _lancamentosClient = new HttpClient { BaseAddress = new Uri(lancamentosBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        _consolidadoClient = new HttpClient { BaseAddress = new Uri(consolidadoBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string> ObterTokenAsync(Guid comercianteId, CancellationToken cancellationToken)
    {
        var response = await _lancamentosClient.PostAsJsonAsync(
            "/dev/token", new EmitirTokenRequest(comercianteId), JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmitirTokenResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Resposta vazia ao solicitar token de desenvolvimento.");

        return payload.Token;
    }

    public async Task<(bool Sucesso, string? Motivo)> CriarLancamentoAsync(
        string token, TipoLancamento tipo, decimal valor, string descricao, DateOnly data, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/lancamentos")
        {
            Content = JsonContent.Create(new CriarLancamentoRequest(tipo, valor, descricao, data), options: JsonOptions)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _lancamentosClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var corpo = await response.Content.ReadAsStringAsync(cancellationToken);
        return (false, $"{(int)response.StatusCode} {response.StatusCode}: {corpo}");
    }

    public async Task<SaldoDiarioResponse?> ConsultarSaldoDiarioAsync(string token, DateOnly data, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/consolidado/saldo-diario?data={data:yyyy-MM-dd}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _consolidadoClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<SaldoDiarioResponse>(JsonOptions, cancellationToken);
    }
}
