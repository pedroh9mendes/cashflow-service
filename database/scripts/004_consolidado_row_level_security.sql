-- Row-Level Security para isolamento multi-tenant (ver docs/adr/ADR-006-multi-tenancy.md)
-- Executar após a aplicação das migrations do EF Core criarem a tabela SaldosDiarios.
-- Mesma estratégia aplicada em Lancamentos.Infrastructure/Sql/001_row_level_security.sql.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Security')
BEGIN
    EXEC('CREATE SCHEMA Security');
END
GO

-- Usa CREATE (não CREATE OR ALTER): uma vez que a SECURITY POLICY abaixo referencia esta
-- função, o SQL Server bloqueia ALTER sobre ela (erro 3729) - idempotência é garantida pelo
-- IF NOT EXISTS, não por reemitir a definição a cada execução do script.
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Security.fn_TenantAccessPredicate') AND type = 'IF')
BEGIN
    EXEC('
        CREATE FUNCTION Security.fn_TenantAccessPredicate(@ComercianteId UNIQUEIDENTIFIER)
        RETURNS TABLE
        WITH SCHEMABINDING
        AS
        RETURN SELECT 1 AS AccessResult
        WHERE @ComercianteId = CAST(SESSION_CONTEXT(N''ComercianteId'') AS UNIQUEIDENTIFIER)
           OR IS_MEMBER(''db_owner'') = 1
    ');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.security_policies WHERE name = 'TenantSecurityPolicy')
BEGIN
    EXEC('
        CREATE SECURITY POLICY Security.TenantSecurityPolicy
        ADD FILTER PREDICATE Security.fn_TenantAccessPredicate(ComercianteId) ON dbo.SaldosDiarios,
        ADD BLOCK PREDICATE Security.fn_TenantAccessPredicate(ComercianteId) ON dbo.SaldosDiarios AFTER INSERT
        WITH (STATE = ON)
    ');
END
GO

-- A aplicação define o contexto de sessão a cada conexão aberta via
-- EXEC sp_set_session_context @key = N'ComercianteId', @value = @ComercianteId.
-- Ver Consolidado.Infrastructure/TenantSessionInterceptor.cs.
-- O consumidor de fila (LancamentoConsumerService) também define esse contexto a cada
-- mensagem processada, usando o ComercianteId do próprio evento como fonte confiável
-- (ver ADR-006, seção "Exceção justificada: consumidor de eventos do Consolidado").
