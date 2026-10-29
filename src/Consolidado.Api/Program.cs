using Consolidado.Api.RateLimiting;
using Consolidado.Domain;
using Consolidado.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using Shared.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Consolidado")
        .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

builder.Services.AddDbContext<ConsolidadoDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConsolidadoDb"));
    options.AddInterceptors(new TenantSessionInterceptor(serviceProvider.GetRequiredService<ITenantContextAccessor>()));
});

builder.Services.AddScoped<ISaldoDiarioRepository, SaldoDiarioRepository>();
builder.Services.AddScoped<IEventoProcessadoRepository, EventoProcessadoRepository>();
builder.Services.AddScoped<AplicarLancamentoService>();
builder.Services.AddHostedService<LancamentoConsumerService>();

builder.Services.AddPlatformJwtAuthentication(builder.Configuration);

builder.Services.AddSingleton<SaturacaoAgregadaTracker>();
builder.Services.AddRateLimiter(TenantElasticRateLimiterPolicy.Configure);

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("ConsolidadoDb")!, name: "sqlserver")
    .AddRabbitMQ(rabbitConnectionString: builder.Configuration.GetConnectionString("RabbitMq")!, name: "rabbitmq");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
    await DatabaseInitializer.InicializarAsync(dbContext, CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UsePlatformAuthentication();

app.UseSaturacaoTracking();
app.UseRateLimiter();

app.UseHttpMetrics();

app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health");

app.Run();
