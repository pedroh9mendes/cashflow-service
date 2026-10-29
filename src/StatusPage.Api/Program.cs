using System.Text.Json.Serialization;
using Serilog;
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

builder.Services.AddHttpClient<StatusPageService>();
builder.Services.AddSingleton<SqlScriptRunner>();
builder.Services.AddSingleton<OrchestrationService>();
builder.Services.AddSingleton<DocumentationService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", async (StatusPageService statusPageService, CancellationToken cancellationToken) =>
{
    var status = await statusPageService.ObterStatusConsolidadoAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapPost("/api/orchestration/apply-sql-scripts", async (OrchestrationService orchestration, CancellationToken cancellationToken) =>
{
    var status = await orchestration.IniciarAsync(AcaoOrquestracao.AplicarScriptsSql, cancellationToken);
    return Results.Ok(status);
});

app.MapPost("/api/orchestration/seed", async (OrchestrationService orchestration, CancellationToken cancellationToken) =>
{
    var status = await orchestration.IniciarAsync(AcaoOrquestracao.PopularDadosDemonstracao, cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/api/orchestration/status/{acao}", (string acao, OrchestrationService orchestration) =>
{
    if (!Enum.TryParse<AcaoOrquestracao>(acao, ignoreCase: true, out var acaoEnum))
    {
        return Results.NotFound();
    }

    return Results.Ok(orchestration.ObterStatus(acaoEnum));
});

app.MapGet("/api/docs/list", (DocumentationService documentation) =>
{
    return Results.Ok(documentation.Listar());
});

app.MapGet("/api/docs/content", async (string id, DocumentationService documentation, CancellationToken cancellationToken) =>
{
    var conteudo = await documentation.ObterConteudoAsync(id, cancellationToken);
    return conteudo is null ? Results.NotFound() : Results.Text(conteudo, "text/markdown; charset=utf-8");
});

app.Run();
