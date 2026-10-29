using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Security;

namespace Lancamentos.Api.Dev;

/// <summary>Dados para emissão de um token JWT de teste para um comerciante específico.</summary>
/// <param name="ComercianteId">Identificador do comerciante (tenant) a representar no token.</param>
public sealed record EmitirTokenRequest(Guid ComercianteId);

/// <summary>Token JWT emitido, pronto para uso no cabeçalho <c>Authorization: Bearer {Token}</c>.</summary>
/// <param name="Token">Token JWT assinado.</param>
/// <param name="ExpiraEm">Data e hora (UTC) de expiração do token.</param>
public sealed record EmitirTokenResponse(string Token, DateTimeOffset ExpiraEm);

/// <summary>
/// Emissão de token JWT para uso em desenvolvimento e testes locais, substituindo um
/// provedor de identidade real (ver docs/architecture/c2-containers.md, subdomínio
/// Identidade/Tenant simplificado para o escopo deste desafio). Nunca deve ser exposto
/// em um ambiente de produção - mapeado apenas quando ASPNETCORE_ENVIRONMENT=Development.
/// </summary>
public static class DevTokenEndpoints
{
    public static void MapDevTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/dev/token", (EmitirTokenRequest request, IOptions<JwtOptions> jwtOptions) =>
        {
            if (request.ComercianteId == Guid.Empty)
            {
                return Results.BadRequest("ComercianteId é obrigatório.");
            }

            var options = jwtOptions.Value;
            var expiraEm = DateTimeOffset.UtcNow.AddHours(8);

            var claims = new[]
            {
                new Claim(TenantResolutionMiddleware.ComercianteClaimType, request.ComercianteId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, request.ComercianteId.ToString())
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: options.Issuer,
                audience: options.Audience,
                claims: claims,
                expires: expiraEm.UtcDateTime,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Results.Ok(new EmitirTokenResponse(tokenString, expiraEm));
        })
        .AllowAnonymous()
        .WithTags("Desenvolvimento")
        .WithSummary("Emite um token JWT de teste para um comerciante (apenas em ambiente de desenvolvimento).")
        .WithDescription("Substitui um provedor de identidade real para fins de demonstração local. " +
            "Nunca é mapeado em produção — disponível apenas quando ASPNETCORE_ENVIRONMENT=Development.")
        .Produces<EmitirTokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }
}
