namespace Consolidado.Api.RateLimiting;

/// <summary>
/// Rastreia a saturação agregada do processo (requisições em andamento) para modular
/// a fatia do pool de capacidade compartilhada disponível a cada tenant no rate limiter
/// elástico (ADR-009). À medida que a saturação se aproxima da faixa vermelha definida em
/// docs/observability.md, o excedente disponível encolhe progressivamente.
/// </summary>
public sealed class SaturacaoAgregadaTracker
{
    private const int CapacidadeMaximaConcorrente = 100;
    private int _requisicoesEmAndamento;

    public double FatorDisponibilidadeExcedente
    {
        get
        {
            var ocupacao = (double)Volatile.Read(ref _requisicoesEmAndamento) / CapacidadeMaximaConcorrente;

            // Faixa verde (< 60%): excedente pleno. Faixa amarela/vermelha: encolhe linearmente até zero.
            if (ocupacao < 0.6)
            {
                return 1.0;
            }

            var fator = 1.0 - ((ocupacao - 0.6) / 0.4);
            return Math.Clamp(fator, 0.0, 1.0);
        }
    }

    public IDisposable RegistrarRequisicao()
    {
        Interlocked.Increment(ref _requisicoesEmAndamento);
        return new Liberador(this);
    }

    private sealed class Liberador : IDisposable
    {
        private readonly SaturacaoAgregadaTracker _tracker;

        public Liberador(SaturacaoAgregadaTracker tracker) => _tracker = tracker;

        public void Dispose() => Interlocked.Decrement(ref _tracker._requisicoesEmAndamento);
    }
}
