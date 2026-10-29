namespace Shared.Security;

/// <summary>
/// Abstrai o acesso ao tenant autenticado da requisição atual, permitindo que
/// componentes de infraestrutura (ex: interceptors de banco) obtenham o tenant
/// sem depender diretamente de HttpContext.
/// </summary>
public interface ITenantContextAccessor
{
    TenantContext? Current { get; }
}

public sealed class HttpTenantContextAccessor : ITenantContextAccessor
{
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContextAccessor(Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public TenantContext? Current
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;

            if (context is not null && context.Items.TryGetValue(nameof(TenantContext), out var value)
                && value is TenantContext tenantContext)
            {
                return tenantContext;
            }

            return null;
        }
    }
}

/// <summary>
/// Implementação para processos sem HttpContext (consumidores de fila, workers), onde o
/// ComercianteId é conhecido a partir de uma fonte já confiável (ex: um evento previamente
/// validado) e não de um request HTTP. Deve ser registrada com escopo por mensagem/unidade de
/// trabalho, nunca como singleton, para que o tenant de uma mensagem não vaze para a próxima
/// (ver ADR-006, seção "Exceção justificada: consumidor de eventos do Consolidado").
/// </summary>
public sealed class AmbientTenantContextAccessor : ITenantContextAccessor
{
    public TenantContext? Current { get; private set; }

    public void Definir(Guid comercianteId)
    {
        Current = new TenantContext(comercianteId);
    }
}

/// <summary>
/// Combina resolução via HttpContext (requisições de API) e via contexto ambiente definido
/// explicitamente (consumidores de fila). Usado em serviços que possuem ambos os fluxos no
/// mesmo processo, como o Consolidado.Api: o HttpContext tem prioridade quando presente;
/// fora de uma requisição, usa o valor definido manualmente via AmbientTenantContextAccessor.
/// </summary>
public sealed class CompositeTenantContextAccessor : ITenantContextAccessor
{
    private readonly HttpTenantContextAccessor _httpAccessor;
    private readonly AmbientTenantContextAccessor _ambientAccessor;

    public CompositeTenantContextAccessor(HttpTenantContextAccessor httpAccessor, AmbientTenantContextAccessor ambientAccessor)
    {
        _httpAccessor = httpAccessor;
        _ambientAccessor = ambientAccessor;
    }

    public TenantContext? Current => _httpAccessor.Current ?? _ambientAccessor.Current;
}
