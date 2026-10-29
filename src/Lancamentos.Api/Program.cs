using Lancamentos.Api.Dev;
using Lancamentos.Api.HealthChecks;
using Lancamentos.Domain;
using Lancamentos.Infrastructure;
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
        .Enrich.WithProperty("Service", "Lancamentos")
        .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));

builder.Services.AddSingleton<ContingencyCircuitBreaker>();

builder.Services.AddDbContext<LancamentosDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("LancamentosDb"));
    options.AddInterceptors(new TenantSessionInterceptor(serviceProvider.GetRequiredService<ITenantContextAccessor>()));
});

builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();
builder.Services.AddScoped<RegistrarLancamentoService>();
builder.Services.AddHostedService<OutboxPublisherService>();

builder.Services.AddPlatformJwtAuthentication(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("LancamentosDb")!, name: "sqlserver")
    .AddRabbitMQ(rabbitConnectionString: builder.Configuration.GetConnectionString("RabbitMq")!, name: "rabbitmq")
    .AddCheck<ContingencyModeHealthCheck>("contingency-mode");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LancamentosDbContext>();
    await DatabaseInitializer.InicializarAsync(dbContext, CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapDevTokenEndpoint();
}

app.UseHttpsRedirection();

app.UsePlatformAuthentication();

app.UseHttpMetrics();

app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health");

app.Run();
