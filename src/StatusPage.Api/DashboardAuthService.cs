using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Security;

namespace StatusPage.Api;

/// <summary>Credenciais informadas na tela de login do painel.</summary>
public sealed record LoginRequest(string Usuario, string Senha);

/// <summary>Token emitido após autenticação bem-sucedida.</summary>
public sealed record LoginResponse(string Token, DateTimeOffset ExpiraEm);

/// <summary>
/// Autentica o operador do painel e emite um token JWT com a claim <c>role=admin</c>, assinado
/// com a mesma chave usada pelas APIs de negócio (ver <see cref="JwtOptions"/>) — reaproveita a
/// infraestrutura de autenticação já existente em vez de criar um mecanismo de sessão separado.
/// Este token nunca carrega a claim de tenant: ele autoriza o painel operacional, não chamadas às
/// APIs de Lançamentos/Consolidado.
/// </summary>
public sealed class DashboardAuthService
{
    public const string RoleClaimType = "role";
    public const string AdminRole = "admin";

    private readonly DashboardAuthOptions _authOptions;
    private readonly JwtOptions _jwtOptions;

    public DashboardAuthService(IOptions<DashboardAuthOptions> authOptions, IOptions<JwtOptions> jwtOptions)
    {
        _authOptions = authOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    public LoginResponse? Autenticar(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrWhiteSpace(request.Senha))
        {
            return null;
        }

        var usuarioConfere = FixedTimeEquals(request.Usuario, _authOptions.Usuario);
        var senhaConfere = FixedTimeEquals(CalcularHash(request.Senha), _authOptions.SenhaHash);

        if (!usuarioConfere || !senhaConfere)
        {
            return null;
        }

        var expiraEm = DateTimeOffset.UtcNow.AddHours(8);

        var claims = new[]
        {
            new Claim(RoleClaimType, AdminRole),
            new Claim(JwtRegisteredClaimNames.Sub, request.Usuario)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiraEm.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse(tokenString, expiraEm);
    }

    private static string CalcularHash(string valor)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(valor));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);

        if (bytesA.Length != bytesB.Length)
        {
            // CryptographicOperations.FixedTimeEquals exige mesmo tamanho; strings de tamanho
            // diferente já não conferem, então o tempo de resposta não vaza nada de útil aqui.
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}
