IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028200219_InitialCreate'
)
BEGIN
    CREATE TABLE [EventosProcessados] (
        [LancamentoId] uniqueidentifier NOT NULL,
        [ProcessadoEm] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EventosProcessados] PRIMARY KEY ([LancamentoId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028200219_InitialCreate'
)
BEGIN
    CREATE TABLE [SaldosDiarios] (
        [ComercianteId] uniqueidentifier NOT NULL,
        [Data] date NOT NULL,
        [TotalCreditos] decimal(18,2) NOT NULL,
        [TotalDebitos] decimal(18,2) NOT NULL,
        [AtualizadoEm] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SaldosDiarios] PRIMARY KEY ([ComercianteId], [Data])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028200219_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028200219_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028201702_AddSaldoDiarioRowVersion'
)
BEGIN
    ALTER TABLE [SaldosDiarios] ADD [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028201702_AddSaldoDiarioRowVersion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028201702_AddSaldoDiarioRowVersion', N'8.0.11');
END;
GO

COMMIT;
GO

