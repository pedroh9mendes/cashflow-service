using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.Security;

namespace Lancamentos.Infrastructure;

/// <summary>
/// Define o SESSION_CONTEXT do SQL Server com o ComercianteId do tenant autenticado
/// a cada conexão aberta, habilitando a política de Row-Level Security (ver Sql/001_row_level_security.sql).
/// </summary>
public sealed class TenantSessionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantSessionInterceptor(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor;
    }

    public override async Task ConnectionOpenedAsync(
        System.Data.Common.DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var tenantContext = _tenantContextAccessor.Current;

        if (tenantContext is not null && connection is SqlConnection sqlConnection)
        {
            await using var command = sqlConnection.CreateCommand();
            command.CommandText = "EXEC sp_set_session_context @key = N'ComercianteId', @value = @ComercianteId";
            command.Parameters.AddWithValue("@ComercianteId", tenantContext.ComercianteId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
