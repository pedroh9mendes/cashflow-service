using Contingencia.ClienteExemplo;

Console.WriteLine("Cliente de exemplo - Camada 3 de resiliência (persistência local na origem)");
Console.WriteLine("Ver docs/architecture/camada-3-contingencia.md para o desenho completo.");
Console.WriteLine();

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5080";
var token = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("CONTINGENCIA_TOKEN");

if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("Informe o token JWT como segundo argumento, ou defina a variável de ambiente CONTINGENCIA_TOKEN.");
    Console.WriteLine("Obtenha um token de teste em POST /dev/token no Lancamentos.Api (ambiente Development).");
    return;
}

var armazenamento = new ArmazenamentoLocal("lancamentos-locais.db");

using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

var clienteReconciliacao = new ClienteReconciliacao(httpClient);

Console.WriteLine("Registrando um lançamento localmente, antes de qualquer tentativa de envio ao backend...");

var registro = new RegistroLocal(
    IdOrigem: Guid.NewGuid(),
    Tipo: TipoLancamentoLocal.Credito,
    Valor: 150.75m,
    Descricao: "Venda de exemplo registrada em modo contingência",
    Data: DateOnly.FromDateTime(DateTime.UtcNow),
    Sincronizado: false);

armazenamento.Registrar(registro);
Console.WriteLine($"Lançamento {registro.IdOrigem} gravado localmente com sucesso. Comerciante não percebe nenhuma falha neste ponto.");
Console.WriteLine();

Console.WriteLine("Tentando sincronizar lançamentos pendentes com o backend...");

var pendentes = armazenamento.ListarPendentes();
var sincronizou = await clienteReconciliacao.SincronizarAsync(pendentes, CancellationToken.None);

if (sincronizou)
{
    foreach (var pendente in pendentes)
    {
        armazenamento.MarcarSincronizado(pendente.IdOrigem);
    }

    Console.WriteLine($"{pendentes.Count} lançamento(s) sincronizado(s) com sucesso. Reconciliação concluída.");
}
else
{
    Console.WriteLine("Backend indisponível no momento. Os lançamentos permanecem pendentes na store local.");
    Console.WriteLine("Execute novamente este programa quando a conectividade for restabelecida - a sincronização é idempotente (via IdOrigem) e não duplicará o lançamento já gravado localmente.");
}
