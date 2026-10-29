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
    WHERE [MigrationId] = N'20261028200149_InitialCreate'
)
BEGIN
    CREATE TABLE [Lancamentos] (
        [Id] uniqueidentifier NOT NULL,
        [ComercianteId] uniqueidentifier NOT NULL,
        [Tipo] int NOT NULL,
        [Valor] decimal(18,2) NOT NULL,
        [Descricao] nvarchar(500) NOT NULL,
        [Data] date NOT NULL,
        [RegistradoEm] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Lancamentos] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028200149_InitialCreate'
)
BEGIN
    CREATE TABLE [OutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [TipoEvento] nvarchar(200) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [CriadoEm] datetimeoffset NOT NULL,
        [PublicadoEm] datetimeoffset NULL,
        [TentativasPublicacao] int NOT NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028200149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Lancamentos_ComercianteId_Data] ON [Lancamentos] ([ComercianteId], [Data]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028200149_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_PublicadoEm] ON [OutboxMessages] ([PublicadoEm]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261028200149_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261028200149_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

