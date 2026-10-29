using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Lancamentos.Infrastructure;

public enum ModoOperacao
{
    Normal,
    Contingencia
}

public sealed record TransicaoModoOperacao(
    ModoOperacao ModoAnterior,
    ModoOperacao ModoAtual,
    string Motivo,
    DateTimeOffset OcorridoEm);

/// <summary>
/// Monitora a capacidade de persistência no banco primário e ativa automaticamente o modo
/// contingência (fallback para o ambiente espelho) ao detectar falha sustentada, com trilha
/// de auditoria de toda transição (Camada 2 de resiliência, ver ADR-007 e ADR-008).
/// </summary>
public sealed class ContingencyCircuitBreaker
{
    private readonly ILogger<ContingencyCircuitBreaker> _logger;
    private readonly List<TransicaoModoOperacao> _historicoAuditoria = new();
    private readonly object _lock = new();

    public ModoOperacao ModoAtual { get; private set; } = ModoOperacao.Normal;

    public IAsyncPolicy Policy { get; }

    public IReadOnlyList<TransicaoModoOperacao> HistoricoAuditoria
    {
        get
        {
            lock (_lock)
            {
                return _historicoAuditoria.ToList();
            }
        }
    }

    public ContingencyCircuitBreaker(ILogger<ContingencyCircuitBreaker> logger)
    {
        _logger = logger;

        Policy = Polly.Policy
            .Handle<Exception>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(30),
                minimumThroughput: 5,
                durationOfBreak: TimeSpan.FromSeconds(15),
                onBreak: (exception, breakDelay) => RegistrarTransicao(ModoOperacao.Contingencia,
                    $"Circuito aberto após falhas sustentadas de conexão com o banco primário: {exception.Message}"),
                onReset: () => RegistrarTransicao(ModoOperacao.Normal,
                    "Circuito fechado - conectividade com o banco primário restabelecida"),
                onHalfOpen: () => _logger.LogInformation(
                    "Circuit breaker em estado semiaberto - testando conectividade com o banco primário"));
    }

    private void RegistrarTransicao(ModoOperacao novoModo, string motivo)
    {
        lock (_lock)
        {
            var transicao = new TransicaoModoOperacao(ModoAtual, novoModo, motivo, DateTimeOffset.UtcNow);
            _historicoAuditoria.Add(transicao);
            ModoAtual = novoModo;

            LancamentosMetrics.CircuitBreakerEstado.Set(novoModo == ModoOperacao.Contingencia ? 1 : 0);

            _logger.LogWarning(
                "Transição de modo de operação: {ModoAnterior} -> {ModoAtual}. Motivo: {Motivo}. Timestamp: {Timestamp}",
                transicao.ModoAnterior, transicao.ModoAtual, transicao.Motivo, transicao.OcorridoEm);
        }
    }
}
