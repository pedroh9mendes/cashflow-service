using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Security;

namespace Lancamentos.Api.Dev;

public sealed record EmitirTokenRequest(Guid ComercianteId);

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
        }).AllowAnonymous();
    }
}
