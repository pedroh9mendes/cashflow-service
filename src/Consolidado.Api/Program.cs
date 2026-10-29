using Consolidado.Api.RateLimiting;
using Consolidado.Domain;
using Consolidado.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
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
        .Enrich.WithProperty("Service", "Consolidado")
        .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CashFlow Service — Consolidado API",
        Version = "v1",
        Description = """
            Serviço de leitura (projeção CQRS) do CashFlow Service: consome eventos de lançamentos
            publicados pela API de Lançamentos via RabbitMQ e expõe o saldo diário consolidado por
            comerciante (tenant), já pré-calculado, otimizado para alta vazão de consultas.

            **Integração com sistemas legados**: use este serviço apenas para leitura do saldo
            diário consolidado. Lançamentos devem sempre ser registrados na Lançamentos API — este
            serviço nunca aceita escrita direta, pois sua projeção é derivada assincronamente dos
            eventos publicados pelo outro serviço.

            **Limite de requisições (rate limiting elástico)**: cada tenant possui um piso mínimo
            garantido de requisições por segundo, podendo consumir capacidade adicional de um pool
            compartilhado quando o sistema não está saturado. Requisições acima do limite retornam
            `429 Too Many Requests` — consulte `docs/security.md` para o desenho completo.

            **Autenticação**: todos os endpoints exigem um token JWT Bearer com a claim
            `comerciante_id`. Use o botão "Authorize" abaixo informando `Bearer {token}` (obtenha um
            token de teste via `POST /dev/token` na Lançamentos API).
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
        Description = "Token JWT no formato: Bearer {seu_token}. Obtenha um token de teste via POST /dev/token na Lançamentos API.",
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
    var dbContext = scope.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
    await DatabaseInitializer.InicializarAsync(dbContext, CancellationToken.None);
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Consolidado API v1");
    options.DocumentTitle = "CashFlow Service — Consolidado API";
});

app.UseHttpsRedirection();

app.UsePlatformAuthentication();

app.UseSaturacaoTracking();
app.UseRateLimiter();

app.UseHttpMetrics();

app.UseCors();

app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health").RequireCors("HealthCheckOrigemAberta");

app.Run();
