using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace StatusPage.Api;

public enum AcaoOrquestracao
{
    AplicarScriptsSql,
    PopularDadosDemonstracao
}

public enum EstadoExecucao
{
    Ociosa,
    EmExecucao,
    Concluida,
    Falhou
}

public sealed record StatusExecucao(AcaoOrquestracao Acao, EstadoExecucao Estado, string Log, DateTimeOffset? IniciadoEm, DateTimeOffset? FinalizadoEm);

/// <summary>
/// Orquestra ações de setup local (scripts SQL e seed de dados) disparadas pelo painel.
/// Cada ação roda no máximo uma vez por vez - uma segunda chamada enquanto a primeira
/// ainda está em execução retorna o estado atual em vez de disparar uma execução paralela.
/// </summary>
public sealed class OrchestrationService
{
    private readonly OrchestrationOptions _options;
    private readonly SqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<OrchestrationService> _logger;
    private readonly Dictionary<AcaoOrquestracao, StatusExecucao> _estados = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public OrchestrationService(
        IOptions<OrchestrationOptions> options,
        SqlScriptRunner sqlScriptRunner,
        ILogger<OrchestrationService> logger)
    {
        _options = options.Value;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public StatusExecucao ObterStatus(AcaoOrquestracao acao)
    {
        return _estados.TryGetValue(acao, out var status)
            ? status
            : new StatusExecucao(acao, EstadoExecucao.Ociosa, string.Empty, null, null);
    }

    public async Task<StatusExecucao> IniciarAsync(AcaoOrquestracao acao, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_estados.TryGetValue(acao, out var atual) && atual.Estado == EstadoExecucao.EmExecucao)
            {
                return atual;
            }

            var iniciado = new StatusExecucao(acao, EstadoExecucao.EmExecucao, "Execução iniciada...", DateTimeOffset.UtcNow, null);
            _estados[acao] = iniciado;
        }
        finally
        {
            _lock.Release();
        }

        _ = Task.Run(() => ExecutarAsync(acao), CancellationToken.None);

        return _estados[acao];
    }

    private async Task ExecutarAsync(AcaoOrquestracao acao)
    {
        try
        {
            var log = acao switch
            {
                AcaoOrquestracao.AplicarScriptsSql => await ExecutarScriptsSqlAsync(),
                AcaoOrquestracao.PopularDadosDemonstracao => await ExecutarSeedAsync(),
                _ => throw new ArgumentOutOfRangeException(nameof(acao))
            };

            _estados[acao] = new StatusExecucao(acao, EstadoExecucao.Concluida, log, _estados[acao].IniciadoEm, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao executar ação de orquestração {Acao}", acao);
            _estados[acao] = new StatusExecucao(acao, EstadoExecucao.Falhou, ex.Message, _estados[acao].IniciadoEm, DateTimeOffset.UtcNow);
        }
    }

    private async Task<string> ExecutarScriptsSqlAsync()
    {
        var resultados = await _sqlScriptRunner.ExecutarTodosAsync(CancellationToken.None);

        var linhas = resultados.Select(r => $"{(r.Sucesso ? "OK" : "ERRO")} - {r.Script}: {r.Mensagem}");
        var log = string.Join("\n", linhas);

        if (resultados.Any(r => !r.Sucesso))
        {
            throw new InvalidOperationException($"Um ou mais scripts falharam.\n{log}");
        }

        return log;
    }

    private async Task<string> ExecutarSeedAsync()
    {
        if (!File.Exists(_options.SeedDllPath))
        {
            throw new FileNotFoundException(
                $"O executável do seed não foi encontrado em {_options.SeedDllPath}. " +
                "Reconstrua a imagem do statuspage-api (docker compose up -d --build statuspage-api).");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{_options.SeedDllPath}\" \"{_options.SeedLancamentosBaseUrl}\" \"{_options.SeedConsolidadoBaseUrl}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        var output = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(15));
        var processTask = process.WaitForExitAsync();

        var concluido = await Task.WhenAny(processTask, timeoutTask);

        if (concluido == timeoutTask)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("O seed excedeu o tempo limite de 15 minutos e foi encerrado.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"O seed finalizou com código {process.ExitCode}.\n{output}");
        }

        return output.ToString();
    }
}
