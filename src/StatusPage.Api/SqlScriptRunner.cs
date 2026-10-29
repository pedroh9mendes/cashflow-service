using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace StatusPage.Api;

public sealed record SqlScriptResult(string Script, bool Sucesso, string Mensagem);

/// <summary>
/// Executa os scripts standalone de database/scripts/ diretamente contra o SQL Server,
/// sem depender do EF Core. Separa os lotes por GO, já que SqlCommand não interpreta
/// esse separador (é uma convenção do sqlcmd/SSMS, não T-SQL real).
/// </summary>
public sealed class SqlScriptRunner
{
    private readonly OrchestrationOptions _options;
    private readonly ILogger<SqlScriptRunner> _logger;

    public SqlScriptRunner(IOptions<OrchestrationOptions> options, ILogger<SqlScriptRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SqlScriptResult>> ExecutarTodosAsync(CancellationToken cancellationToken)
    {
        var resultados = new List<SqlScriptResult>();

        await GarantirBancosCriadosAsync(cancellationToken);

        foreach (var nomeArquivo in _options.SqlScriptsOrder)
        {
            var caminho = Path.Combine(_options.SqlScriptsPath, nomeArquivo);

            if (!File.Exists(caminho))
            {
                resultados.Add(new SqlScriptResult(nomeArquivo, false, $"Arquivo não encontrado em {caminho}"));
                continue;
            }

            var banco = nomeArquivo.StartsWith("001") || nomeArquivo.StartsWith("003")
                ? "LancamentosDb"
                : "ConsolidadoDb";

            try
            {
                var conteudo = await File.ReadAllTextAsync(caminho, cancellationToken);
                await ExecutarScriptAsync(conteudo, banco, cancellationToken);
                resultados.Add(new SqlScriptResult(nomeArquivo, true, $"Aplicado com sucesso em {banco}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao executar o script {Script}", nomeArquivo);
                resultados.Add(new SqlScriptResult(nomeArquivo, false, ex.Message));
            }
        }

        return resultados;
    }

    private async Task GarantirBancosCriadosAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_options.SqlServerConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var banco in new[] { "LancamentosDb", "ConsolidadoDb" })
        {
            var comando = connection.CreateCommand();
            comando.CommandText =
                $"IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '{banco}') CREATE DATABASE [{banco}];";
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task ExecutarScriptAsync(string conteudoScript, string banco, CancellationToken cancellationToken)
    {
        var connectionString = new SqlConnectionStringBuilder(_options.SqlServerConnectionString)
        {
            InitialCatalog = banco
        }.ConnectionString;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var lote in DividirPorGo(conteudoScript))
        {
            if (string.IsNullOrWhiteSpace(lote))
            {
                continue;
            }

            var comando = connection.CreateCommand();
            comando.CommandText = lote;
            comando.CommandTimeout = 60;
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static IEnumerable<string> DividirPorGo(string script)
    {
        var linhas = script.Split('\n');
        var loteAtual = new StringBuilder();

        foreach (var linha in linhas)
        {
            if (linha.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                yield return loteAtual.ToString();
                loteAtual.Clear();
                continue;
            }

            loteAtual.AppendLine(linha);
        }

        if (loteAtual.Length > 0)
        {
            yield return loteAtual.ToString();
        }
    }
}
