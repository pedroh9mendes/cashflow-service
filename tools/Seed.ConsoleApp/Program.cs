using System.Diagnostics;
using Seed.ConsoleApp;

var lancamentosBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5080";
var consolidadoBaseUrl = args.Length > 1 ? args[1] : "http://localhost:5090";

Console.WriteLine("Seed de dados de demonstracao - Plataforma de Fluxo de Caixa");
Console.WriteLine($"Lancamentos.Api: {lancamentosBaseUrl}");
Console.WriteLine($"Consolidado.Api: {consolidadoBaseUrl}");
Console.WriteLine();

var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
var inicio = hoje.AddDays(-29);

// Dias de pico simulados: geram volume elevado para a maioria dos comerciantes no mesmo dia,
// util para visualizar o card de saturacao do dashboard (ver docs/observability.md).
var diasDePico = new HashSet<DateOnly>
{
    inicio.AddDays(5),
    inicio.AddDays(14),
    inicio.AddDays(24)
};

Console.WriteLine($"Periodo: {inicio:yyyy-MM-dd} a {hoje:yyyy-MM-dd}");
Console.WriteLine($"Dias de pico simulados: {string.Join(", ", diasDePico.Select(d => d.ToString("yyyy-MM-dd")))}");
Console.WriteLine($"Comerciantes na carteira: {CarteiraComerciantes.Todos.Count}");
Console.WriteLine();

var client = new ApiClient(lancamentosBaseUrl, consolidadoBaseUrl);
var random = new Random(Seed: 20261029); // seed fixa para reprodutibilidade entre execucoes

using var semaforo = new SemaphoreSlim(3);
var cronometro = Stopwatch.StartNew();

var totalCriados = 0;
var totalFalhas = 0;
var lockContadores = new object();

foreach (var comerciante in CarteiraComerciantes.Todos)
{
    string token;

    try
    {
        token = await client.ObterTokenAsync(comerciante.Id, CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERRO] Falha ao obter token para {comerciante.Nome}: {ex.Message}");
        continue;
    }

    var tarefas = new List<Task>();
    var criadosComerciante = 0;
    var falhasComerciante = 0;

    for (var data = inicio; data <= hoje; data = data.AddDays(1))
    {
        var (min, max) = comerciante.FaixaLancamentosPorDia;
        var quantidadeBase = random.Next(min, max + 1);
        var quantidade = diasDePico.Contains(data) ? (int)(quantidadeBase * 2.5) : quantidadeBase;

        for (var i = 0; i < quantidade; i++)
        {
            var ehCredito = random.NextDouble() < 0.6;
            var catalogo = ehCredito ? DescricaoLancamento.Creditos : DescricaoLancamento.Debitos;
            var item = catalogo[random.Next(catalogo.Length)];
            var valor = Math.Round((decimal)random.NextDouble() * (item.Max - item.Min) + item.Min, 2);
            var dataLancamento = data;

            await semaforo.WaitAsync();

            tarefas.Add(Task.Run(async () =>
            {
                try
                {
                    var (sucesso, motivo) = await client.CriarLancamentoAsync(
                        token,
                        ehCredito ? TipoLancamento.Credito : TipoLancamento.Debito,
                        valor,
                        item.Descricao,
                        dataLancamento,
                        CancellationToken.None);

                    lock (lockContadores)
                    {
                        if (sucesso)
                        {
                            totalCriados++;
                            criadosComerciante++;
                        }
                        else
                        {
                            totalFalhas++;
                            falhasComerciante++;

                            if (falhasComerciante <= 3)
                            {
                                Console.WriteLine($"[FALHA] {comerciante.Nome} em {dataLancamento:yyyy-MM-dd}: {motivo}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (lockContadores)
                    {
                        totalFalhas++;
                        falhasComerciante++;
                    }

                    Console.WriteLine($"[ERRO] {comerciante.Nome} em {dataLancamento:yyyy-MM-dd}: {ex.Message}");
                }
                finally
                {
                    semaforo.Release();
                }
            }));
        }
    }

    await Task.WhenAll(tarefas);

    Console.WriteLine(
        $"{comerciante.Nome} ({comerciante.Porte}): {criadosComerciante} lancamento(s) criado(s)" +
        (falhasComerciante > 0 ? $", {falhasComerciante} falha(s)" : string.Empty));
}

cronometro.Stop();

Console.WriteLine();
Console.WriteLine("=== Resumo do seed ===");
Console.WriteLine($"Total de lancamentos criados: {totalCriados}");
Console.WriteLine($"Total de falhas: {totalFalhas}");
Console.WriteLine($"Tempo total: {cronometro.Elapsed:mm\\:ss}");
Console.WriteLine();

Console.WriteLine("Aguardando propagacao assincrona (Outbox -> RabbitMQ -> projecao de saldo)...");
await Task.Delay(TimeSpan.FromSeconds(10));

Console.WriteLine();
Console.WriteLine("=== Validacao pos-seed: saldo consolidado de amostra ===");

var amostras = new[]
{
    (CarteiraComerciantes.Todos[0], hoje),
    (CarteiraComerciantes.Todos[4], inicio.AddDays(5)),
    (CarteiraComerciantes.Todos[9], inicio.AddDays(14)),
};

foreach (var (comerciante, data) in amostras)
{
    var token = await client.ObterTokenAsync(comerciante.Id, CancellationToken.None);
    var saldo = await client.ConsultarSaldoDiarioAsync(token, data, CancellationToken.None);

    if (saldo is null)
    {
        Console.WriteLine($"{comerciante.Nome} em {data:yyyy-MM-dd}: saldo ainda nao disponivel.");
        continue;
    }

    Console.WriteLine(
        $"{comerciante.Nome} em {data:yyyy-MM-dd}: creditos={saldo.TotalCreditos:C}, " +
        $"debitos={saldo.TotalDebitos:C}, saldo={saldo.SaldoFinal:C}");
}

Console.WriteLine();
Console.WriteLine("Seed concluido.");
