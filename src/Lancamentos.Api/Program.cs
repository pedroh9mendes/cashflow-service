using Lancamentos.Api.Dev;
using Lancamentos.Api.HealthChecks;
using Lancamentos.Domain;
using Lancamentos.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Shared.Security;
using System.Reflection;

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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CashFlow Service — Lançamentos API",
        Version = "v1",
        Description = """
            Serviço de escrita do CashFlow Service: registra débitos e créditos do fluxo de caixa
            de cada comerciante (tenant) e publica eventos de forma confiável (Outbox Pattern) para
            o serviço de Consolidado Diário via RabbitMQ.

            **Integração com sistemas legados**: este serviço é o ponto de entrada recomendado para
            qualquer sistema externo (legado ou de terceiros) que precise registrar lançamentos
            financeiros na plataforma. O endpoint `POST /api/lancamentos/reconciliar` foi desenhado
            especificamente para sincronização em lote de lançamentos gerados offline ou em um
            sistema legado (ver Camada 3 de resiliência em `docs/architecture/camada-3-contingencia.md`),
            com idempotência garantida por `IdOrigem`.

            **Autenticação**: todos os endpoints (exceto `/dev/token`, disponível só em ambiente de
            desenvolvimento) exigem um token JWT Bearer com a claim `comerciante_id`. Use o botão
            "Authorize" abaixo informando `Bearer {token}`.
            """,
        Contact = new OpenApiContact
        {
            Name = "CashFlow Service — Documentação completa",
            Url = new Uri("http://localhost:5100/#documentacao")
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Token JWT no formato: Bearer {seu_token}. Obtenha um token de teste em POST /dev/token.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    var contractsXmlPath = Path.Combine(AppContext.BaseDirectory, "Shared.Contracts.xml");
    if (File.Exists(contractsXmlPath))
    {
        options.IncludeXmlComments(contractsXmlPath);
    }
});

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

// CORS liberado apenas para o /health: a instalação assistida (onboarding/onboarding.html) é um
// arquivo estático aberto fora desta origem (ex: file://, ou outra máquina na rede) e precisa
// verificar se o serviço já subiu antes de orientar o próximo passo. O restante da API continua
// sem CORS - não há necessidade de chamada browser-to-browser nos endpoints de negócio.
builder.Services.AddCors(options =>
{
    options.AddPolicy("HealthCheckOrigemAberta", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LancamentosDbContext>();
    await DatabaseInitializer.InicializarAsync(dbContext, CancellationToken.None);
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Lançamentos API v1");
    options.DocumentTitle = "CashFlow Service — Lançamentos API";
});

if (app.Environment.IsDevelopment())
{
    app.MapDevTokenEndpoint();
}

app.UseHttpsRedirection();

app.UsePlatformAuthentication();

app.UseHttpMetrics();

app.UseCors();

app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health").RequireCors("HealthCheckOrigemAberta");

app.Run();
