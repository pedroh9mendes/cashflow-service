namespace Consolidado.Api.RateLimiting;

public sealed class SaturacaoTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public SaturacaoTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SaturacaoAgregadaTracker tracker)
    {
        using (tracker.RegistrarRequisicao())
        {
            await _next(context);
        }
    }
}

public static class SaturacaoTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseSaturacaoTracking(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SaturacaoTrackingMiddleware>();
    }
}
