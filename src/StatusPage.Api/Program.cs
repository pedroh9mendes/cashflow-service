using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.Security;
using StatusPage.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "StatusPage")
        .WriteTo.Console();
});

builder.Services.Configure<StatusPageOptions>(builder.Configuration.GetSection(StatusPageOptions.SectionName));
builder.Services.Configure<OrchestrationOptions>(builder.Configuration.GetSection(OrchestrationOptions.SectionName));
builder.Services.Configure<DocumentationOptions>(builder.Configuration.GetSection(DocumentationOptions.SectionName));
builder.Services.Configure<DashboardAuthOptions>(builder.Configuration.GetSection(DashboardAuthOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddHttpClient<StatusPageService>();
builder.Services.AddHttpClient<MetricasAgregadasService>();
builder.Services.AddSingleton<SqlScriptRunner>();
builder.Services.AddSingleton<OrchestrationService>();
builder.Services.AddSingleton<DocumentationService>();
builder.Services.AddSingleton<DashboardAuthService>();

// O painel operacional (tudo exceto /health, /api/status e /status/*, que são públicos) exige
// um token JWT com a claim role=admin, emitido por POST /api/auth/login. Reaproveita a mesma
// infraestrutura JWT das APIs de negócio (mesma chave de assinatura), mas este token nunca
// carrega claim de tenant - não serve para chamar Lançamentos/Consolidado.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Desliga o remapeamento automático de claims curtas (ex: "role") para URIs longas do
        // WS-Federation que o JwtSecurityTokenHandler aplica por padrão - sem isso, a claim
        // "role" emitida pelo DashboardAuthService chega ao ClaimsPrincipal com outro nome e a
        // policy PainelAdmin nunca bate, mesmo com um token válido (causava 403 mesmo logado).
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PainelAdmin", policy =>
        policy.RequireClaim(DashboardAuthService.RoleClaimType, DashboardAuthService.AdminRole));
});

// CORS liberado apenas para o /health: a instalação assistida (onboarding/onboarding.html) é um
// arquivo estático aberto fora desta origem (ex: file://, ou outra máquina na rede) e precisa
// verificar se este serviço já subiu antes de orientar o próximo passo.
builder.Services.AddCors(options =>
{
    options.AddPolicy("HealthCheckOrigemAberta", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/auth/login", (LoginRequest request, DashboardAuthService authService) =>
{
    var resultado = authService.Autenticar(request);
    return resultado is null ? Results.Unauthorized() : Results.Ok(resultado);
});

app.MapGet("/api/status", async (StatusPageService statusPageService, CancellationToken cancellationToken) =>
{
    var status = await statusPageService.ObterStatusConsolidadoAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).RequireCors("HealthCheckOrigemAberta");

// Todo o restante do painel (métricas, orquestração, documentação) exige o token de administrador
// emitido por /api/auth/login - são dados e ações internas, nunca expostos à página pública /status.
var painel = app.MapGroup("/api").RequireAuthorization("PainelAdmin");

painel.MapGet("/metricas", async (MetricasAgregadasService metricasService, CancellationToken cancellationToken) =>
{
    var metricas = await metricasService.ObterAsync(cancellationToken);
    return Results.Ok(metricas);
});

painel.MapGet("/metricas/serie-diaria", async (MetricasAgregadasService metricasService, CancellationToken cancellationToken) =>
{
    var serie = await metricasService.ObterSerieDiariaAsync(cancellationToken);
    return serie is null ? Results.Ok(new { pontos = Array.Empty<object>() }) : Results.Text(serie.Value.GetRawText(), "application/json");
});

painel.MapGet("/orchestration/bloqueio", async (OrchestrationService orchestration, CancellationToken cancellationToken) =>
{
    var bloqueio = await orchestration.VerificarBloqueioAsync(cancellationToken);
    return Results.Ok(bloqueio);
});

painel.MapGet("/orchestration/defaults", (IOptions<OrchestrationOptions> options) =>
{
    var o = options.Value;
    return Results.Ok(new
    {
        o.SqlServerConnectionString,
        o.SeedLancamentosBaseUrl,
        o.SeedConsolidadoBaseUrl
    });
});

painel.MapPost("/orchestration/apply-sql-scripts", async (ParametrosExecucao? parametros, OrchestrationService orchestration, CancellationToken cancellationToken) =>
{
    try
    {
        var status = await orchestration.IniciarAsync(AcaoOrquestracao.AplicarScriptsSql, parametros ?? new ParametrosExecucao(), cancellationToken);
        return Results.Ok(status);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { mensagem = ex.Message });
    }
});

painel.MapPost("/orchestration/seed", async (ParametrosExecucao? parametros, OrchestrationService orchestration, CancellationToken cancellationToken) =>
{
    try
    {
        var status = await orchestration.IniciarAsync(AcaoOrquestracao.PopularDadosDemonstracao, parametros ?? new ParametrosExecucao(), cancellationToken);
        return Results.Ok(status);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { mensagem = ex.Message });
    }
});

painel.MapGet("/orchestration/status/{acao}", (string acao, OrchestrationService orchestration) =>
{
    if (!Enum.TryParse<AcaoOrquestracao>(acao, ignoreCase: true, out var acaoEnum))
    {
        return Results.NotFound();
    }

    return Results.Ok(orchestration.ObterStatus(acaoEnum));
});

painel.MapGet("/docs/list", (DocumentationService documentation) =>
{
    return Results.Ok(documentation.Listar());
});

painel.MapGet("/docs/content", async (string id, DocumentationService documentation, CancellationToken cancellationToken) =>
{
    var conteudo = await documentation.ObterConteudoAsync(id, cancellationToken);
    return conteudo is null ? Results.NotFound() : Results.Text(conteudo, "text/markdown; charset=utf-8");
});

app.Run();
