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
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [CorrelationId] varchar(128) NOT NULL,
        [StaffId] varchar(20) NOT NULL,
        [Role] varchar(30) NOT NULL,
        [BranchCode] varchar(10) NOT NULL,
        [Controller] varchar(100) NOT NULL,
        [Action] varchar(100) NOT NULL,
        [Parameters] nvarchar(2000) NOT NULL,
        [StatusCode] int NOT NULL,
        [DurationMs] bigint NOT NULL,
        [TimestampUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] int NOT NULL IDENTITY,
        [BranchCode] varchar(10) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Region] nvarchar(50) NOT NULL,
        [OpenTime] time NOT NULL,
        [CloseTime] time NOT NULL,
        [TimeZoneId] varchar(64) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Branches_BranchCode] UNIQUE ([BranchCode]),
        CONSTRAINT [CK_Branches_Hours] CHECK ([OpenTime] < [CloseTime])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [Id] int NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] int NOT NULL,
        [Name] varchar(30) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingZones] (
        [Id] varchar(20) NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [Multiplier] decimal(6,3) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ShippingZones_Multiplier] CHECK ([Multiplier] > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [Counters] (
        [Id] int NOT NULL IDENTITY,
        [BranchCode] varchar(10) NOT NULL,
        [TerminalCode] varchar(10) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Counters] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Counters_Branch_Terminal] UNIQUE ([BranchCode], [TerminalCode]),
        CONSTRAINT [FK_Counters_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [Sku] varchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CategoryId] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxCategory] varchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_Products_Sku] UNIQUE ([Sku]),
        CONSTRAINT [CK_Products_TaxCategory] CHECK ([TaxCategory] IN ('Exempt','StandardGst','Reduced')),
        CONSTRAINT [CK_Products_UnitPrice] CHECK ([UnitPrice] > 0),
        CONSTRAINT [FK_Products_ProductCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ProductCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [Staff] (
        [Id] varchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Email] varchar(254) NOT NULL,
        [RoleId] int NOT NULL,
        [HomeBranchCode] varchar(10) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [PasswordHash] varchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Staff] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Staff_Branches_HomeBranchCode] FOREIGN KEY ([HomeBranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Staff_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingRates] (
        [Id] int NOT NULL,
        [ZoneId] varchar(20) NOT NULL,
        [IsParcel] bit NOT NULL,
        [ServiceLevel] varchar(20) NOT NULL,
        [BaseRate] decimal(18,2) NOT NULL,
        [MaxWeightKg] decimal(9,3) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ShippingRates] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ShippingRates_BaseRate] CHECK ([BaseRate] > 0),
        CONSTRAINT [CK_ShippingRates_MaxWeight] CHECK ([MaxWeightKg] > 0 AND [MaxWeightKg] <= 20),
        CONSTRAINT [CK_ShippingRates_ServiceLevel] CHECK ([ServiceLevel] IN ('Standard','Express','Registered')),
        CONSTRAINT [FK_ShippingRates_ShippingZones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [ShippingZones] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [BranchStock] (
        [Id] uniqueidentifier NOT NULL,
        [BranchCode] varchar(10) NOT NULL,
        [Sku] varchar(20) NOT NULL,
        [QuantityOnHand] int NOT NULL DEFAULT 0,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BranchStock] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_BranchStock_Quantity] CHECK ([QuantityOnHand] >= 0),
        CONSTRAINT [FK_BranchStock_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BranchStock_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [CounterTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [BranchCode] varchar(10) NOT NULL,
        [CounterCode] varchar(10) NOT NULL,
        [ClerkStaffId] varchar(20) NOT NULL,
        [Status] varchar(20) NOT NULL DEFAULT 'Open',
        [OccurredAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [BusinessDate] date NOT NULL,
        [VoidReason] nvarchar(200) NULL,
        [VoidedByStaffId] varchar(20) NULL,
        [InventoryRestocked] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CounterTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CounterTransactions_Status] CHECK ([Status] IN ('Open','Paid','Voided','Refunded')),
        CONSTRAINT [FK_CounterTransactions_Counters_BranchCode_CounterCode] FOREIGN KEY ([BranchCode], [CounterCode]) REFERENCES [Counters] ([BranchCode], [TerminalCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Staff_ClerkStaffId] FOREIGN KEY ([ClerkStaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CounterTransactions_Staff_VoidedByStaffId] FOREIGN KEY ([VoidedByStaffId]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [StockAdjustments] (
        [Id] bigint NOT NULL IDENTITY,
        [BranchCode] varchar(10) NOT NULL,
        [Sku] varchar(20) NOT NULL,
        [Delta] int NOT NULL,
        [QuantityAfter] int NOT NULL,
        [Reason] varchar(30) NOT NULL,
        [ChangedBy] varchar(20) NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_StockAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockAdjustments_Delta] CHECK ([Delta] <> 0),
        CONSTRAINT [CK_StockAdjustments_QuantityAfter] CHECK ([QuantityAfter] >= 0),
        CONSTRAINT [CK_StockAdjustments_Reason] CHECK ([Reason] IN ('Replenishment','Damage','CountCorrection','CustomerReturn')),
        CONSTRAINT [FK_StockAdjustments_Branches_BranchCode] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockAdjustments_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockAdjustments_Staff_ChangedBy] FOREIGN KEY ([ChangedBy]) REFERENCES [Staff] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [TransactionId] uniqueidentifier NOT NULL,
        [Kind] varchar(10) NOT NULL DEFAULT 'Sale',
        [Method] varchar(10) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaidAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [CK_Payments_Kind] CHECK ([Kind] IN ('Sale','Void','Refund')),
        CONSTRAINT [CK_Payments_Method] CHECK ([Method] IN ('Cash','Card','EFTPOS')),
        CONSTRAINT [FK_Payments_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [TransactionLines] (
        [Id] uniqueidentifier NOT NULL,
        [TransactionId] uniqueidentifier NOT NULL,
        [LineNumber] int NOT NULL,
        [Description] nvarchar(200) NOT NULL,
        [LineType] varchar(30) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxCategory] varchar(20) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [Sku] varchar(20) NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_TransactionLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TransactionLines_Amounts] CHECK ([UnitPrice] >= 0 AND [TaxAmount] >= 0 AND [LineTotal] >= 0),
        CONSTRAINT [CK_TransactionLines_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_TransactionLines_RetailSku] CHECK (([LineType] = 'RetailProduct' AND [Sku] IS NOT NULL) OR ([LineType] <> 'RetailProduct' AND [Sku] IS NULL)),
        CONSTRAINT [CK_TransactionLines_Type] CHECK ([LineType] IN ('RetailProduct','ParcelShipping','MoneyOrder','IdentityVerification')),
        CONSTRAINT [FK_TransactionLines_CounterTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TransactionLines_Products_Sku] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [IdentityVerifications] (
        [Id] uniqueidentifier NOT NULL,
        [TransactionLineId] uniqueidentifier NOT NULL,
        [DocumentType] varchar(30) NOT NULL,
        [MaskedDocumentNumber] varchar(10) NOT NULL,
        [IsVerified] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_IdentityVerifications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_IdentityVerifications_DocumentType] CHECK ([DocumentType] IN ('Passport','DriversLicense','NationalIdentityCard')),
        CONSTRAINT [CK_IdentityVerifications_Masked] CHECK ([MaskedDocumentNumber] LIKE '******____'),
        CONSTRAINT [FK_IdentityVerifications_TransactionLines_TransactionLineId] FOREIGN KEY ([TransactionLineId]) REFERENCES [TransactionLines] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [MoneyOrders] (
        [Id] uniqueidentifier NOT NULL,
        [TransactionLineId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Fee] decimal(18,2) NOT NULL,
        [Sender] nvarchar(100) NOT NULL,
        [Recipient] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_MoneyOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MoneyOrders_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_MoneyOrders_Fee] CHECK ([Fee] >= 0),
        CONSTRAINT [FK_MoneyOrders_TransactionLines_TransactionLineId] FOREIGN KEY ([TransactionLineId]) REFERENCES [TransactionLines] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE TABLE [Shipments] (
        [Id] uniqueidentifier NOT NULL,
        [TransactionLineId] uniqueidentifier NOT NULL,
        [ShippingRateId] int NOT NULL,
        [WeightKg] decimal(9,3) NOT NULL,
        [LengthCm] decimal(9,2) NOT NULL,
        [WidthCm] decimal(9,2) NOT NULL,
        [HeightCm] decimal(9,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(50) NULL DEFAULT 'system',
        [ModifiedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Shipments_Weight] CHECK ([WeightKg] > 0 AND [WeightKg] <= 20),
        CONSTRAINT [FK_Shipments_ShippingRates_ShippingRateId] FOREIGN KEY ([ShippingRateId]) REFERENCES [ShippingRates] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Shipments_TransactionLines_TransactionLineId] FOREIGN KEY ([TransactionLineId]) REFERENCES [TransactionLines] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[ProductCategories]'))
        SET IDENTITY_INSERT [ProductCategories] ON;
    EXEC(N'INSERT INTO [ProductCategories] ([Id], [Name])
    VALUES (1, N''Packaging''),
    (2, N''Stationery''),
    (3, N''Office Supplies'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[ProductCategories]'))
        SET IDENTITY_INSERT [ProductCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [Name])
    VALUES (0, ''CounterClerk''),
    (1, ''BranchManager''),
    (2, ''Auditor''),
    (3, ''Administrator'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ModifiedAtUtc', N'Multiplier', N'Name') AND [object_id] = OBJECT_ID(N'[ShippingZones]'))
        SET IDENTITY_INSERT [ShippingZones] ON;
    EXEC(N'INSERT INTO [ShippingZones] ([Id], [ModifiedAtUtc], [Multiplier], [Name])
    VALUES (''Zone 1'', NULL, 1.0, N''Zone 1''),
    (''Zone 10'', NULL, 2.0, N''Zone 10''),
    (''Zone 2'', NULL, 1.0, N''Zone 2''),
    (''Zone 3'', NULL, 1.25, N''Zone 3''),
    (''Zone 4'', NULL, 1.25, N''Zone 4''),
    (''Zone 5'', NULL, 1.5, N''Zone 5''),
    (''Zone 6'', NULL, 1.5, N''Zone 6''),
    (''Zone 7'', NULL, 1.75, N''Zone 7''),
    (''Zone 8'', NULL, 1.75, N''Zone 8''),
    (''Zone 9'', NULL, 2.0, N''Zone 9'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ModifiedAtUtc', N'Multiplier', N'Name') AND [object_id] = OBJECT_ID(N'[ShippingZones]'))
        SET IDENTITY_INSERT [ShippingZones] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BaseRate', N'IsParcel', N'MaxWeightKg', N'ModifiedAtUtc', N'ServiceLevel', N'ZoneId') AND [object_id] = OBJECT_ID(N'[ShippingRates]'))
        SET IDENTITY_INSERT [ShippingRates] ON;
    EXEC(N'INSERT INTO [ShippingRates] ([Id], [BaseRate], [IsParcel], [MaxWeightKg], [ModifiedAtUtc], [ServiceLevel], [ZoneId])
    VALUES (1, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 1''),
    (2, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 1''),
    (3, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 1''),
    (4, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 1''),
    (5, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 1''),
    (6, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 1''),
    (7, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 2''),
    (8, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 2''),
    (9, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 2''),
    (10, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 2''),
    (11, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 2''),
    (12, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 2''),
    (13, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 3''),
    (14, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 3''),
    (15, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 3''),
    (16, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 3''),
    (17, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 3''),
    (18, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 3''),
    (19, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 4''),
    (20, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 4''),
    (21, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 4''),
    (22, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 4''),
    (23, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 4''),
    (24, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 4''),
    (25, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 5''),
    (26, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 5''),
    (27, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 5''),
    (28, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 5''),
    (29, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 5''),
    (30, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 5''),
    (31, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 6''),
    (32, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 6''),
    (33, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 6''),
    (34, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 6''),
    (35, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 6''),
    (36, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 6''),
    (37, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 7''),
    (38, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 7''),
    (39, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 7''),
    (40, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 7''),
    (41, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 7''),
    (42, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 7'');
    INSERT INTO [ShippingRates] ([Id], [BaseRate], [IsParcel], [MaxWeightKg], [ModifiedAtUtc], [ServiceLevel], [ZoneId])
    VALUES (43, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 8''),
    (44, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 8''),
    (45, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 8''),
    (46, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 8''),
    (47, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 8''),
    (48, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 8''),
    (49, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 9''),
    (50, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 9''),
    (51, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 9''),
    (52, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 9''),
    (53, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 9''),
    (54, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 9''),
    (55, 1.5, CAST(0 AS bit), 0.5, NULL, ''Standard'', ''Zone 10''),
    (56, 4.5, CAST(0 AS bit), 0.5, NULL, ''Express'', ''Zone 10''),
    (57, 6.0, CAST(0 AS bit), 0.5, NULL, ''Registered'', ''Zone 10''),
    (58, 10.0, CAST(1 AS bit), 20.0, NULL, ''Standard'', ''Zone 10''),
    (59, 18.0, CAST(1 AS bit), 10.0, NULL, ''Express'', ''Zone 10''),
    (60, 24.0, CAST(1 AS bit), 2.0, NULL, ''Registered'', ''Zone 10'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BaseRate', N'IsParcel', N'MaxWeightKg', N'ModifiedAtUtc', N'ServiceLevel', N'ZoneId') AND [object_id] = OBJECT_ID(N'[ShippingRates]'))
        SET IDENTITY_INSERT [ShippingRates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BranchStock_Sku] ON [BranchStock] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_BranchStock_Branch_Sku] ON [BranchStock] ([BranchCode], [Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_BranchCode_CounterCode] ON [CounterTransactions] ([BranchCode], [CounterCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_ClerkStaffId] ON [CounterTransactions] ([ClerkStaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_VoidedByStaffId] ON [CounterTransactions] ([VoidedByStaffId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IdentityVerifications_TransactionLineId] ON [IdentityVerifications] ([TransactionLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MoneyOrders_TransactionLineId] ON [MoneyOrders] ([TransactionLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_TransactionId] ON [Payments] ([TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductCategories_Name] ON [ProductCategories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_ShippingRateId] ON [Shipments] ([ShippingRateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Shipments_TransactionLineId] ON [Shipments] ([TransactionLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ShippingRates_Zone_Type_Level] ON [ShippingRates] ([ZoneId], [IsParcel], [ServiceLevel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ShippingZones_Name] ON [ShippingZones] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Staff_HomeBranchCode] ON [Staff] ([HomeBranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Staff_RoleId] ON [Staff] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Staff_Email] ON [Staff] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_BranchCode] ON [StockAdjustments] ([BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_ChangedBy] ON [StockAdjustments] ([ChangedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_Sku] ON [StockAdjustments] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_Sku] ON [TransactionLines] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_TransactionLines_Transaction_Number] ON [TransactionLines] ([TransactionId], [LineNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060746_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031060746_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    DROP INDEX [IX_StockAdjustments_BranchCode] ON [StockAdjustments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    DROP INDEX [IX_Shipments_ShippingRateId] ON [Shipments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_StockAdjustments_Branch_Sku_Time] ON [StockAdjustments] ([BranchCode], [Sku], [ChangedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_ShippingRates_Zone_Level] ON [ShippingRates] ([ZoneId], [ServiceLevel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_Shipments_Rate] ON [Shipments] ([ShippingRateId]) INCLUDE ([TransactionLineId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_MoneyOrders_HighValue] ON [MoneyOrders] ([Amount]) INCLUDE ([Fee], [TransactionLineId]) WHERE [Amount] > 1000');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_Branch_Date] ON [CounterTransactions] ([BranchCode], [BusinessDate]) INCLUDE ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_BranchStock_Branch_Quantity] ON [BranchStock] ([BranchCode], [QuantityOnHand]) INCLUDE ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_Branch_Time] ON [AuditEntries] ([BranchCode], [TimestampUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_Staff_Time] ON [AuditEntries] ([StaffId], [TimestampUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060813_AddWorkloadIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031060813_AddWorkloadIndexes', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060815_AddStoredProcedures'
)
BEGIN
    EXEC(N'-- Retail vs services totals for one trading day. @BranchCode = NULL returns every branch (the auditor view).
    -- A money order''s principal is customer money, so only its fee counts as service revenue.
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @BranchCode varchar(10) = NULL,
        @Date date
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @Date IS NULL
            THROW 50001, N''@Date is required.'', 1;

        IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE BranchCode = @BranchCode)
            THROW 50404, N''The branch was not found.'', 1;

        SELECT
            t.BranchCode,
            CAST(SUM(CASE WHEN l.LineType = ''RetailProduct'' THEN l.LineTotal ELSE 0 END) AS decimal(18,2)) AS RetailTotal,
            CAST(SUM(CASE
                WHEN l.LineType = ''RetailProduct'' THEN 0
                WHEN l.LineType = ''MoneyOrder'' THEN m.Fee
                ELSE l.LineTotal END) AS decimal(18,2)) AS ServicesTotal
        FROM dbo.CounterTransactions AS t
        INNER JOIN dbo.TransactionLines AS l ON l.TransactionId = t.Id
        LEFT JOIN dbo.MoneyOrders AS m ON m.TransactionLineId = l.Id
        WHERE t.BusinessDate = @Date
          AND t.Status = ''Paid''
          AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
        GROUP BY t.BranchCode
        ORDER BY t.BranchCode
        OPTION (RECOMPILE);
    END
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060815_AddStoredProcedures'
)
BEGIN
    EXEC(N'-- Products at or below @Threshold, optionally for one branch.
    -- OPTION (RECOMPILE) is deliberate: one cached plan cannot suit both "one branch" (a seek) and "all branches"
    -- (a scan). The report runs rarely enough that compile cost is acceptable. See PERFORMANCE.md for the alternatives.
    CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
        @BranchCode varchar(10) = NULL,
        @Threshold int
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @Threshold IS NULL OR @Threshold < 0
            THROW 50002, N''@Threshold must be zero or greater.'', 1;

        IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE BranchCode = @BranchCode)
            THROW 50404, N''The branch was not found.'', 1;

        SELECT s.BranchCode, s.Sku, p.Name AS ProductName, s.QuantityOnHand
        FROM dbo.BranchStock AS s
        INNER JOIN dbo.Products AS p ON p.Sku = s.Sku
        WHERE s.QuantityOnHand <= @Threshold
          AND (@BranchCode IS NULL OR s.BranchCode = @BranchCode)
        ORDER BY s.BranchCode, s.QuantityOnHand, s.Sku
        OPTION (RECOMPILE);
    END
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060815_AddStoredProcedures'
)
BEGIN
    EXEC(N'-- Changes a stock level and writes its history row as one atomic unit, then returns the new quantity.
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode varchar(10),
        @Sku varchar(20),
        @Delta int,
        @Reason varchar(30),
        @ChangedBy varchar(20)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF @BranchCode IS NULL OR @Sku IS NULL OR @ChangedBy IS NULL
            THROW 50003, N''@BranchCode, @Sku and @ChangedBy are required.'', 1;
        IF @Delta IS NULL OR @Delta = 0
            THROW 50004, N''@Delta must be a non-zero quantity.'', 1;
        IF @Reason IS NULL OR @Reason NOT IN (''Replenishment'', ''Damage'', ''CountCorrection'', ''CustomerReturn'')
            THROW 50005, N''@Reason is not a recognised adjustment reason.'', 1;

        DECLARE @Updated TABLE (QuantityOnHand int NOT NULL);

        BEGIN TRY
            BEGIN TRANSACTION;

            -- Same access order as a sale: BranchStock first, then history, which keeps lock order consistent.
            UPDATE dbo.BranchStock
            SET QuantityOnHand = QuantityOnHand + @Delta,
                ModifiedAtUtc = SYSUTCDATETIME()
            OUTPUT inserted.QuantityOnHand INTO @Updated
            WHERE BranchCode = @BranchCode
              AND Sku = @Sku
              AND QuantityOnHand + @Delta >= 0;

            IF NOT EXISTS (SELECT 1 FROM @Updated)
            BEGIN
                IF EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                    THROW 50006, N''Branch stock cannot go negative.'', 1;
                THROW 50404, N''This product is not stocked at this branch.'', 1;
            END;

            INSERT dbo.StockAdjustments (BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy)
            SELECT @BranchCode, @Sku, @Delta, QuantityOnHand, @Reason, @ChangedBy
            FROM @Updated;

            COMMIT TRANSACTION;

            SELECT QuantityOnHand FROM @Updated;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;
            THROW;
        END CATCH;
    END
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031060815_AddStoredProcedures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031060815_AddStoredProcedures', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [StockAdjustments] ADD [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [StockAdjustments] ADD [CreatedBy] varchar(50) NULL DEFAULT 'system';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [StockAdjustments] ADD [ModifiedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [Roles] ADD [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [Roles] ADD [CreatedBy] varchar(50) NULL DEFAULT 'system';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [Roles] ADD [ModifiedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [ProductCategories] ADD [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [ProductCategories] ADD [CreatedBy] varchar(50) NULL DEFAULT 'system';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [ProductCategories] ADD [ModifiedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [AuditEntries] ADD [CreatedAtUtc] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [AuditEntries] ADD [CreatedBy] varchar(50) NULL DEFAULT 'system';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    ALTER TABLE [AuditEntries] ADD [ModifiedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    EXEC(N'UPDATE [ProductCategories] SET [ModifiedAtUtc] = NULL
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    EXEC(N'UPDATE [ProductCategories] SET [ModifiedAtUtc] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    EXEC(N'UPDATE [ProductCategories] SET [ModifiedAtUtc] = NULL
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    EXEC(N'UPDATE [Roles] SET [ModifiedAtUtc] = NULL
    WHERE [Id] = 0;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    EXEC(N'UPDATE [Roles] SET [ModifiedAtUtc] = NULL
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    EXEC(N'UPDATE [Roles] SET [ModifiedAtUtc] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    EXEC(N'UPDATE [Roles] SET [ModifiedAtUtc] = NULL
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031070844_AddRemainingAuditColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031070844_AddRemainingAuditColumns', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031072657_ReplaceLowStockIndexWithFiltered'
)
BEGIN
    DROP INDEX [IX_BranchStock_Branch_Quantity] ON [BranchStock];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031072657_ReplaceLowStockIndexWithFiltered'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_BranchStock_LowStock] ON [BranchStock] ([BranchCode], [Sku]) INCLUDE ([QuantityOnHand]) WHERE [QuantityOnHand] <= 5');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031072657_ReplaceLowStockIndexWithFiltered'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031072657_ReplaceLowStockIndexWithFiltered', N'10.0.14');
END;

COMMIT;
GO

