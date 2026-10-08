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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [Accounts] (
        [Id] nvarchar(200) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Role] nvarchar(max) NOT NULL,
        [Unit] nvarchar(max) NOT NULL,
        [ProviderId] int NULL,
        [Active] bit NOT NULL,
        CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] bigint NOT NULL IDENTITY,
        [OutboxId] bigint NOT NULL,
        [CaseId] int NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [At] datetime2 NOT NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [Outbox] (
        [Id] bigint NOT NULL IDENTITY,
        [CaseId] int NOT NULL,
        [Message] nvarchar(max) NOT NULL,
        [Status] nvarchar(450) NOT NULL,
        [Attempts] int NOT NULL,
        [NextAttempt] datetime2 NOT NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_Outbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [Processes] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(80) NOT NULL,
        [Number] int NOT NULL,
        [DefinitionJson] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Processes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [Providers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Registration] nvarchar(32) NOT NULL,
        [Unit] nvarchar(max) NOT NULL,
        [Branches] nvarchar(max) NOT NULL,
        [ContactName] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Phone] nvarchar(max) NOT NULL,
        [Agreement] nvarchar(max) NOT NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_Providers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [Cases] (
        [Id] int NOT NULL IDENTITY,
        [ProviderId] int NOT NULL,
        [ProcessVersionId] int NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [State] nvarchar(450) NOT NULL,
        [Unit] nvarchar(450) NOT NULL,
        [AssigneeId] nvarchar(max) NULL,
        [DataJson] nvarchar(max) NOT NULL,
        [Round] int NOT NULL,
        [Version] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Cases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Cases_Processes_ProcessVersionId] FOREIGN KEY ([ProcessVersionId]) REFERENCES [Processes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Cases_Providers_ProviderId] FOREIGN KEY ([ProviderId]) REFERENCES [Providers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [Documents] (
        [Id] int NOT NULL IDENTITY,
        [CaseId] int NOT NULL,
        [Kind] nvarchar(100) NOT NULL,
        [Number] int NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [StorageKey] nvarchar(max) NOT NULL,
        [Size] bigint NOT NULL,
        [ValidUntil] datetime2 NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Documents_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [Cases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [History] (
        [Id] int NOT NULL IDENTITY,
        [CaseId] int NOT NULL,
        [Actor] nvarchar(max) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Note] nvarchar(max) NOT NULL,
        [Round] int NOT NULL,
        [At] datetime2 NOT NULL,
        CONSTRAINT [PK_History] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_History_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [Cases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE TABLE [Tasks] (
        [Id] int NOT NULL IDENTITY,
        [CaseId] int NOT NULL,
        [DocumentId] int NOT NULL,
        [Round] int NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Result] nvarchar(max) NULL,
        [ReviewerId] nvarchar(max) NULL,
        [Note] nvarchar(max) NOT NULL,
        [DueAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Tasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tasks_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [Cases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE INDEX [IX_Cases_ProcessVersionId] ON [Cases] ([ProcessVersionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE INDEX [IX_Cases_ProviderId] ON [Cases] ([ProviderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE INDEX [IX_Cases_Unit_State_UpdatedAt] ON [Cases] ([Unit], [State], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Documents_CaseId_Kind_Number] ON [Documents] ([CaseId], [Kind], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE INDEX [IX_History_CaseId] ON [History] ([CaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Notifications_OutboxId] ON [Notifications] ([OutboxId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE INDEX [IX_Outbox_Status_NextAttempt] ON [Outbox] ([Status], [NextAttempt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Processes_Key_Number] ON [Processes] ([Key], [Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Providers_Registration] ON [Providers] ([Registration]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tasks_CaseId_Round_DocumentId] ON [Tasks] ([CaseId], [Round], [DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004192419_InitialWorkflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004192419_InitialWorkflow', N'9.0.2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    ALTER TABLE [Outbox] ADD [Kind] nvarchar(max) NOT NULL DEFAULT N'notification';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    ALTER TABLE [Outbox] ADD [Recipient] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    ALTER TABLE [Accounts] ADD [ReadAccess] nvarchar(16) NOT NULL DEFAULT N'subtree';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    ALTER TABLE [Accounts] ADD [Version] bigint NOT NULL DEFAULT CAST(1 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    ALTER TABLE [Accounts] ADD [WriteAccess] nvarchar(16) NOT NULL DEFAULT N'subtree';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    CREATE TABLE [OrgUnits] (
        [Key] nvarchar(80) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [ParentKey] nvarchar(80) NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_OrgUnits] PRIMARY KEY ([Key]),
        CONSTRAINT [FK_OrgUnits_OrgUnits_ParentKey] FOREIGN KEY ([ParentKey]) REFERENCES [OrgUnits] ([Key]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    CREATE TABLE [RoutingDecisions] (
        [Id] int NOT NULL IDENTITY,
        [CaseId] int NOT NULL,
        [RuleId] int NULL,
        [RuleVersion] bigint NULL,
        [RuleName] nvarchar(max) NOT NULL,
        [Unit] nvarchar(max) NOT NULL,
        [UnitName] nvarchar(max) NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [At] datetime2 NOT NULL,
        CONSTRAINT [PK_RoutingDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoutingDecisions_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [Cases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    CREATE TABLE [RoutingRules] (
        [Id] int NOT NULL IDENTITY,
        [ProcessKey] nvarchar(80) NULL,
        [Name] nvarchar(max) NOT NULL,
        [Priority] int NOT NULL,
        [Enabled] bit NOT NULL,
        [TargetUnit] nvarchar(80) NOT NULL,
        [SpecJson] nvarchar(max) NOT NULL,
        [Version] bigint NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RoutingRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoutingRules_OrgUnits_TargetUnit] FOREIGN KEY ([TargetUnit]) REFERENCES [OrgUnits] ([Key]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    CREATE INDEX [IX_OrgUnits_ParentKey] ON [OrgUnits] ([ParentKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoutingDecisions_CaseId] ON [RoutingDecisions] ([CaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    CREATE INDEX [IX_RoutingRules_Enabled_Priority] ON [RoutingRules] ([Enabled], [Priority]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    CREATE INDEX [IX_RoutingRules_TargetUnit] ON [RoutingRules] ([TargetUnit]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005092145_OrganizationAccessAndProcessRouting'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005092145_OrganizationAccessAndProcessRouting', N'9.0.2');
END;

COMMIT;
GO

