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
/// Resultado da checagem de segurança que decide se as ações de orquestração podem rodar.
/// Dois motivos de bloqueio, verificados sempre no servidor (não apenas escondidos no painel):
/// a trava explícita de ambiente de produção, e os serviços de negócio (Lançamentos/Consolidado)
/// estarem respondendo — aplicar scripts/popular dados com a aplicação no ar arrisca corromper
/// dados reais em uso.
/// </summary>
public sealed record BloqueioOrquestracao(bool Bloqueado, string? Motivo);

/// <summary>
/// Parâmetros de ambiente informados explicitamente pelo operador antes de disparar uma ação
/// de orquestração. Nenhum valor aqui é assumido silenciosamente: o painel sempre exibe o
/// default local para confirmação, mas o operador pode apontar para outro servidor (ex: ambiente
/// do cliente) antes de executar.
/// </summary>
public sealed record ParametrosExecucao(
    string? SqlServerConnectionString = null,
    string? SeedLancamentosBaseUrl = null,
    string? SeedConsolidadoBaseUrl = null);

/// <summary>
/// Orquestra ações de setup local (scripts SQL e seed de dados) disparadas pelo painel.
/// Cada ação roda no máximo uma vez por vez - uma segunda chamada enquanto a primeira
/// ainda está em execução retorna o estado atual em vez de disparar uma execução paralela.
/// </summary>
public sealed class OrchestrationService
{
    private readonly OrchestrationOptions _options;
    private readonly SqlScriptRunner _sqlScriptRunner;
    private readonly StatusPageService _statusPageService;
    private readonly ILogger<OrchestrationService> _logger;
    private readonly Dictionary<AcaoOrquestracao, StatusExecucao> _estados = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public OrchestrationService(
        IOptions<OrchestrationOptions> options,
        SqlScriptRunner sqlScriptRunner,
        StatusPageService statusPageService,
        ILogger<OrchestrationService> logger)
    {
        _options = options.Value;
        _sqlScriptRunner = sqlScriptRunner;
        _statusPageService = statusPageService;
        _logger = logger;
    }

    public StatusExecucao ObterStatus(AcaoOrquestracao acao)
    {
        return _estados.TryGetValue(acao, out var status)
            ? status
            : new StatusExecucao(acao, EstadoExecucao.Ociosa, string.Empty, null, null);
    }

    /// <summary>
    /// Verifica se as ações de orquestração podem rodar neste momento. Bloqueia incondicionalmente
    /// se a trava de ambiente de produção estiver ativa, e bloqueia também se Lançamentos ou
    /// Consolidado estiverem respondendo — scripts de schema e seed de dados não devem rodar
    /// contra uma aplicação em uso.
    /// </summary>
    public async Task<BloqueioOrquestracao> VerificarBloqueioAsync(CancellationToken cancellationToken)
    {
        if (_options.AmbienteProducao)
        {
            return new BloqueioOrquestracao(true,
                "Este ambiente está marcado como produção (Orchestration__AmbienteProducao=true). " +
                "Scripts de schema e seed de dados de demonstração nunca são executados pelo painel em produção.");
        }

        var status = await _statusPageService.ObterStatusConsolidadoAsync(cancellationToken);
        var servicosNoAr = status.Where(s => s.Status != StatusServico.Indisponivel).ToList();

        if (servicosNoAr.Count > 0)
        {
            var nomes = string.Join(" e ", servicosNoAr.Select(s => s.Servico));
            var verbo = servicosNoAr.Count > 1 ? "estão" : "está";
            return new BloqueioOrquestracao(true,
                $"{nomes} ainda {verbo} no ar. Pare a aplicação antes de aplicar scripts de banco ou popular dados " +
                "de demonstração — executar essas ações com a aplicação em uso pode corromper dados reais. " +
                "Execute: docker compose stop lancamentos-api consolidado-api");
        }

        return new BloqueioOrquestracao(false, null);
    }

    public async Task<StatusExecucao> IniciarAsync(AcaoOrquestracao acao, ParametrosExecucao parametros, CancellationToken cancellationToken)
    {
        var bloqueio = await VerificarBloqueioAsync(cancellationToken);
        if (bloqueio.Bloqueado)
        {
            throw new InvalidOperationException(bloqueio.Motivo);
        }

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

        _ = Task.Run(() => ExecutarAsync(acao, parametros), CancellationToken.None);

        return _estados[acao];
    }

    private async Task ExecutarAsync(AcaoOrquestracao acao, ParametrosExecucao parametros)
    {
        try
        {
            var log = acao switch
            {
                AcaoOrquestracao.AplicarScriptsSql => await ExecutarScriptsSqlAsync(parametros),
                AcaoOrquestracao.PopularDadosDemonstracao => await ExecutarSeedAsync(parametros),
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

    private async Task<string> ExecutarScriptsSqlAsync(ParametrosExecucao parametros)
    {
        var connectionString = string.IsNullOrWhiteSpace(parametros.SqlServerConnectionString)
            ? _options.SqlServerConnectionString
            : parametros.SqlServerConnectionString;

        var resultados = await _sqlScriptRunner.ExecutarTodosAsync(connectionString, CancellationToken.None);

        var linhas = resultados.Select(r => $"{(r.Sucesso ? "OK" : "ERRO")} - {r.Script}: {r.Mensagem}");
        var log = string.Join("\n", linhas);

        if (resultados.Any(r => !r.Sucesso))
        {
            throw new InvalidOperationException($"Um ou mais scripts falharam.\n{log}");
        }

        return log;
    }

    private async Task<string> ExecutarSeedAsync(ParametrosExecucao parametros)
    {
        if (!File.Exists(_options.SeedDllPath))
        {
            throw new FileNotFoundException(
                $"O executável do seed não foi encontrado em {_options.SeedDllPath}. " +
                "Reconstrua a imagem do statuspage-api (docker compose up -d --build statuspage-api).");
        }

        var lancamentosBaseUrl = string.IsNullOrWhiteSpace(parametros.SeedLancamentosBaseUrl)
            ? _options.SeedLancamentosBaseUrl
            : parametros.SeedLancamentosBaseUrl;

        var consolidadoBaseUrl = string.IsNullOrWhiteSpace(parametros.SeedConsolidadoBaseUrl)
            ? _options.SeedConsolidadoBaseUrl
            : parametros.SeedConsolidadoBaseUrl;

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{_options.SeedDllPath}\" \"{lancamentosBaseUrl}\" \"{consolidadoBaseUrl}\"",
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
