using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Security;

namespace Consolidado.Api.RateLimiting;

/// <summary>
/// Implementa a quota elástica por tenant descrita no ADR-009: cada comerciante possui um
/// piso mínimo garantido de requisições por segundo, podendo consumir além disso a partir de
/// um pool de capacidade compartilhada ociosa, até um teto máximo individual. O excedente
/// disponível no pool compartilhado encolhe conforme a saturação agregada do sistema aumenta,
/// protegendo o piso garantido de todos os tenants (ver docs/observability.md - card de saturação).
/// </summary>
public static class TenantElasticRateLimiterPolicy
{
    public const string PolicyName = "tenant-elastic";

    private const int PisoGarantidoPorSegundo = 5;
    private const int TetoMaximoPorTenantPorSegundo = 30;
    private const int CapacidadeCompartilhadaTotal = 40;

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy<string, TenantElasticPartitionPolicy>(PolicyName);
    }

    public sealed class TenantElasticPartitionPolicy : IRateLimiterPolicy<string>
    {
        private readonly SaturacaoAgregadaTracker _saturacaoTracker;

        public TenantElasticPartitionPolicy(SaturacaoAgregadaTracker saturacaoTracker)
        {
            _saturacaoTracker = saturacaoTracker;
        }

        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            var comercianteId = httpContext.Items.TryGetValue(nameof(TenantContext), out var value)
                && value is TenantContext tenantContext
                ? tenantContext.ComercianteId.ToString()
                : "anonimo";

            var fatorSaturacao = _saturacaoTracker.FatorDisponibilidadeExcedente;
            var limiteEfetivo = PisoGarantidoPorSegundo +
                (int)Math.Round((TetoMaximoPorTenantPorSegundo - PisoGarantidoPorSegundo) * fatorSaturacao);

            return RateLimitPartition.GetTokenBucketLimiter(comercianteId, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = Math.Max(PisoGarantidoPorSegundo, limiteEfetivo),
                TokensPerPeriod = Math.Max(PisoGarantidoPorSegundo, limiteEfetivo),
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        }
    }
}
