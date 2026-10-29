using Lancamentos.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lancamentos.Api.HealthChecks;

/// <summary>
/// Reporta Degraded quando o serviço está em modo contingência (Camada 2, ver ADR-008),
/// alimentando o endpoint /health consumido pela status page pública (ADR-011).
/// </summary>
public sealed class ContingencyModeHealthCheck : IHealthCheck
{
    private readonly ContingencyCircuitBreaker _circuitBreaker;

    public ContingencyModeHealthCheck(ContingencyCircuitBreaker circuitBreaker)
    {
        _circuitBreaker = circuitBreaker;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var resultado = _circuitBreaker.ModoAtual == ModoOperacao.Contingencia
            ? HealthCheckResult.Degraded("Serviço operando em modo contingência (gravando no ambiente espelho).")
            : HealthCheckResult.Healthy("Operando normalmente no ambiente primário.");

        return Task.FromResult(resultado);
    }
}
