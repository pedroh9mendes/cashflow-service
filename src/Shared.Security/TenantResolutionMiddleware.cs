using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Shared.Security;

/// <summary>
/// Extrai o ComercianteId exclusivamente do claim autenticado do token.
/// Nenhum endpoint de negócio deve aceitar ComercianteId vindo de rota, query string ou corpo da requisição,
/// conforme documentado em docs/security.md (prevenção de IDOR).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string ComercianteClaimType = "comerciante_id";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claimValue = context.User.FindFirstValue(ComercianteClaimType);

            if (!string.IsNullOrEmpty(claimValue) && Guid.TryParse(claimValue, out var comercianteId))
            {
                context.Items[nameof(TenantContext)] = new TenantContext(comercianteId);
            }
        }

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }

    public static TenantContext GetTenantContext(this HttpContext context)
    {
        if (context.Items.TryGetValue(nameof(TenantContext), out var value) && value is TenantContext tenantContext)
        {
            return tenantContext;
        }

        throw new UnauthorizedAccessException("Contexto de tenant não resolvido para a requisição atual.");
    }
}
