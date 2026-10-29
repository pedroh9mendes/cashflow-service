using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Security;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddPlatformJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
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

        services.AddAuthorization();
        services.AddHttpContextAccessor();

        // Registrado por escopo (não singleton): cada requisição HTTP ou cada unidade de
        // trabalho de um worker (ex: uma mensagem de fila) tem seu próprio tenant resolvido,
        // nunca compartilhado entre chamadas concorrentes (ver ADR-006).
        services.AddScoped<HttpTenantContextAccessor>();
        services.AddScoped<AmbientTenantContextAccessor>();
        services.AddScoped<ITenantContextAccessor, CompositeTenantContextAccessor>();

        return services;
    }

    public static IApplicationBuilder UsePlatformAuthentication(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseTenantResolution();
        app.UseAuthorization();

        return app;
    }
}
