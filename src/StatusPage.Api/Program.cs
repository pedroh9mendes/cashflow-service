using Serilog;
using StatusPage.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "StatusPage")
        .WriteTo.Console();
});

builder.Services.Configure<StatusPageOptions>(builder.Configuration.GetSection(StatusPageOptions.SectionName));
builder.Services.AddHttpClient<StatusPageService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/status", async (StatusPageService statusPageService, CancellationToken cancellationToken) =>
{
    var status = await statusPageService.ObterStatusConsolidadoAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
