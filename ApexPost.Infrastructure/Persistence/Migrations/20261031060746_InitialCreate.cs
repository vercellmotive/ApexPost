using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(128)", nullable: false),
                    StaffId = table.Column<string>(type: "varchar(20)", nullable: false),
                    Role = table.Column<string>(type: "varchar(30)", nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    Controller = table.Column<string>(type: "varchar(100)", nullable: false),
                    Action = table.Column<string>(type: "varchar(100)", nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(2000)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    TimeZoneId = table.Column<string>(type: "varchar(64)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.UniqueConstraint("UQ_Branches_BranchCode", x => x.BranchCode);
                    table.CheckConstraint("CK_Branches_Hours", "[OpenTime] < [CloseTime]");
                });

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShippingZones",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(20)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Multiplier = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingZones", x => x.Id);
                    table.CheckConstraint("CK_ShippingZones_Multiplier", "[Multiplier] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Counters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    TerminalCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counters", x => x.Id);
                    table.UniqueConstraint("UQ_Counters_Branch_Terminal", x => new { x.BranchCode, x.TerminalCode });
                    table.ForeignKey(
                        name: "FK_Counters_Branches_BranchCode",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sku = table.Column<string>(type: "varchar(20)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxCategory = table.Column<string>(type: "varchar(20)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.UniqueConstraint("UQ_Products_Sku", x => x.Sku);
                    table.CheckConstraint("CK_Products_TaxCategory", "[TaxCategory] IN ('Exempt','StandardGst','Reduced')");
                    table.CheckConstraint("CK_Products_UnitPrice", "[UnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_Products_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(20)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Email = table.Column<string>(type: "varchar(254)", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    HomeBranchCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PasswordHash = table.Column<string>(type: "varchar(200)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Staff_Branches_HomeBranchCode",
                        column: x => x.HomeBranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Staff_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShippingRates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ZoneId = table.Column<string>(type: "varchar(20)", nullable: false),
                    IsParcel = table.Column<bool>(type: "bit", nullable: false),
                    ServiceLevel = table.Column<string>(type: "varchar(20)", nullable: false),
                    BaseRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxWeightKg = table.Column<decimal>(type: "decimal(9,3)", precision: 9, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingRates", x => x.Id);
                    table.CheckConstraint("CK_ShippingRates_BaseRate", "[BaseRate] > 0");
                    table.CheckConstraint("CK_ShippingRates_MaxWeight", "[MaxWeightKg] > 0 AND [MaxWeightKg] <= 20");
                    table.CheckConstraint("CK_ShippingRates_ServiceLevel", "[ServiceLevel] IN ('Standard','Express','Registered')");
                    table.ForeignKey(
                        name: "FK_ShippingRates_ShippingZones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "ShippingZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchStock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    Sku = table.Column<string>(type: "varchar(20)", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchStock", x => x.Id);
                    table.CheckConstraint("CK_BranchStock_Quantity", "[QuantityOnHand] >= 0");
                    table.ForeignKey(
                        name: "FK_BranchStock_Branches_BranchCode",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchStock_Products_Sku",
                        column: x => x.Sku,
                        principalTable: "Products",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CounterTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    CounterCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    ClerkStaffId = table.Column<string>(type: "varchar(20)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "Open"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VoidReason = table.Column<string>(type: "nvarchar(200)", nullable: true),
                    VoidedByStaffId = table.Column<string>(type: "varchar(20)", nullable: true),
                    InventoryRestocked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CounterTransactions", x => x.Id);
                    table.CheckConstraint("CK_CounterTransactions_Status", "[Status] IN ('Open','Paid','Voided','Refunded')");
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Counters_BranchCode_CounterCode",
                        columns: x => new { x.BranchCode, x.CounterCode },
                        principalTable: "Counters",
                        principalColumns: new[] { "BranchCode", "TerminalCode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Staff_ClerkStaffId",
                        column: x => x.ClerkStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CounterTransactions_Staff_VoidedByStaffId",
                        column: x => x.VoidedByStaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchCode = table.Column<string>(type: "varchar(10)", nullable: false),
                    Sku = table.Column<string>(type: "varchar(20)", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "varchar(30)", nullable: false),
                    ChangedBy = table.Column<string>(type: "varchar(20)", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.Id);
                    table.CheckConstraint("CK_StockAdjustments_Delta", "[Delta] <> 0");
                    table.CheckConstraint("CK_StockAdjustments_QuantityAfter", "[QuantityAfter] >= 0");
                    table.CheckConstraint("CK_StockAdjustments_Reason", "[Reason] IN ('Replenishment','Damage','CountCorrection','CustomerReturn')");
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Branches_BranchCode",
                        column: x => x.BranchCode,
                        principalTable: "Branches",
                        principalColumn: "BranchCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Products_Sku",
                        column: x => x.Sku,
                        principalTable: "Products",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Staff_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "varchar(10)", nullable: false, defaultValue: "Sale"),
                    Method = table.Column<string>(type: "varchar(10)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_Payments_Kind", "[Kind] IN ('Sale','Void','Refund')");
                    table.CheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Card','EFTPOS')");
                    table.ForeignKey(
                        name: "FK_Payments_CounterTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    LineType = table.Column<string>(type: "varchar(30)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxCategory = table.Column<string>(type: "varchar(20)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sku = table.Column<string>(type: "varchar(20)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionLines", x => x.Id);
                    table.CheckConstraint("CK_TransactionLines_Amounts", "[UnitPrice] >= 0 AND [TaxAmount] >= 0 AND [LineTotal] >= 0");
                    table.CheckConstraint("CK_TransactionLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_TransactionLines_RetailSku", "([LineType] = 'RetailProduct' AND [Sku] IS NOT NULL) OR ([LineType] <> 'RetailProduct' AND [Sku] IS NULL)");
                    table.CheckConstraint("CK_TransactionLines_Type", "[LineType] IN ('RetailProduct','ParcelShipping','MoneyOrder','IdentityVerification')");
                    table.ForeignKey(
                        name: "FK_TransactionLines_CounterTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "CounterTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionLines_Products_Sku",
                        column: x => x.Sku,
                        principalTable: "Products",
                        principalColumn: "Sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdentityVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "varchar(30)", nullable: false),
                    MaskedDocumentNumber = table.Column<string>(type: "varchar(10)", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityVerifications", x => x.Id);
                    table.CheckConstraint("CK_IdentityVerifications_DocumentType", "[DocumentType] IN ('Passport','DriversLicense','NationalIdentityCard')");
                    table.CheckConstraint("CK_IdentityVerifications_Masked", "[MaskedDocumentNumber] LIKE '******____'");
                    table.ForeignKey(
                        name: "FK_IdentityVerifications_TransactionLines_TransactionLineId",
                        column: x => x.TransactionLineId,
                        principalTable: "TransactionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MoneyOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Sender = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoneyOrders", x => x.Id);
                    table.CheckConstraint("CK_MoneyOrders_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_MoneyOrders_Fee", "[Fee] >= 0");
                    table.ForeignKey(
                        name: "FK_MoneyOrders_TransactionLines_TransactionLineId",
                        column: x => x.TransactionLineId,
                        principalTable: "TransactionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShippingRateId = table.Column<int>(type: "int", nullable: false),
                    WeightKg = table.Column<decimal>(type: "decimal(9,3)", nullable: false),
                    LengthCm = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    WidthCm = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    HeightCm = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "varchar(50)", nullable: true, defaultValue: "system"),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.CheckConstraint("CK_Shipments_Weight", "[WeightKg] > 0 AND [WeightKg] <= 20");
                    table.ForeignKey(
                        name: "FK_Shipments_ShippingRates_ShippingRateId",
                        column: x => x.ShippingRateId,
                        principalTable: "ShippingRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shipments_TransactionLines_TransactionLineId",
                        column: x => x.TransactionLineId,
                        principalTable: "TransactionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ProductCategories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Packaging" },
                    { 2, "Stationery" },
                    { 3, "Office Supplies" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 0, "CounterClerk" },
                    { 1, "BranchManager" },
                    { 2, "Auditor" },
                    { 3, "Administrator" }
                });

            migrationBuilder.InsertData(
                table: "ShippingZones",
                columns: new[] { "Id", "ModifiedAtUtc", "Multiplier", "Name" },
                values: new object[,]
                {
                    { "Zone 1", null, 1.0m, "Zone 1" },
                    { "Zone 10", null, 2.0m, "Zone 10" },
                    { "Zone 2", null, 1.0m, "Zone 2" },
                    { "Zone 3", null, 1.25m, "Zone 3" },
                    { "Zone 4", null, 1.25m, "Zone 4" },
                    { "Zone 5", null, 1.5m, "Zone 5" },
                    { "Zone 6", null, 1.5m, "Zone 6" },
                    { "Zone 7", null, 1.75m, "Zone 7" },
                    { "Zone 8", null, 1.75m, "Zone 8" },
                    { "Zone 9", null, 2.0m, "Zone 9" }
                });

            migrationBuilder.InsertData(
                table: "ShippingRates",
                columns: new[] { "Id", "BaseRate", "IsParcel", "MaxWeightKg", "ModifiedAtUtc", "ServiceLevel", "ZoneId" },
                values: new object[,]
                {
                    { 1, 1.50m, false, 0.5m, null, "Standard", "Zone 1" },
                    { 2, 4.50m, false, 0.5m, null, "Express", "Zone 1" },
                    { 3, 6.00m, false, 0.5m, null, "Registered", "Zone 1" },
                    { 4, 10.00m, true, 20m, null, "Standard", "Zone 1" },
                    { 5, 18.00m, true, 10m, null, "Express", "Zone 1" },
                    { 6, 24.00m, true, 2m, null, "Registered", "Zone 1" },
                    { 7, 1.50m, false, 0.5m, null, "Standard", "Zone 2" },
                    { 8, 4.50m, false, 0.5m, null, "Express", "Zone 2" },
                    { 9, 6.00m, false, 0.5m, null, "Registered", "Zone 2" },
                    { 10, 10.00m, true, 20m, null, "Standard", "Zone 2" },
                    { 11, 18.00m, true, 10m, null, "Express", "Zone 2" },
                    { 12, 24.00m, true, 2m, null, "Registered", "Zone 2" },
                    { 13, 1.50m, false, 0.5m, null, "Standard", "Zone 3" },
                    { 14, 4.50m, false, 0.5m, null, "Express", "Zone 3" },
                    { 15, 6.00m, false, 0.5m, null, "Registered", "Zone 3" },
                    { 16, 10.00m, true, 20m, null, "Standard", "Zone 3" },
                    { 17, 18.00m, true, 10m, null, "Express", "Zone 3" },
                    { 18, 24.00m, true, 2m, null, "Registered", "Zone 3" },
                    { 19, 1.50m, false, 0.5m, null, "Standard", "Zone 4" },
                    { 20, 4.50m, false, 0.5m, null, "Express", "Zone 4" },
                    { 21, 6.00m, false, 0.5m, null, "Registered", "Zone 4" },
                    { 22, 10.00m, true, 20m, null, "Standard", "Zone 4" },
                    { 23, 18.00m, true, 10m, null, "Express", "Zone 4" },
                    { 24, 24.00m, true, 2m, null, "Registered", "Zone 4" },
                    { 25, 1.50m, false, 0.5m, null, "Standard", "Zone 5" },
                    { 26, 4.50m, false, 0.5m, null, "Express", "Zone 5" },
                    { 27, 6.00m, false, 0.5m, null, "Registered", "Zone 5" },
                    { 28, 10.00m, true, 20m, null, "Standard", "Zone 5" },
                    { 29, 18.00m, true, 10m, null, "Express", "Zone 5" },
                    { 30, 24.00m, true, 2m, null, "Registered", "Zone 5" },
                    { 31, 1.50m, false, 0.5m, null, "Standard", "Zone 6" },
                    { 32, 4.50m, false, 0.5m, null, "Express", "Zone 6" },
                    { 33, 6.00m, false, 0.5m, null, "Registered", "Zone 6" },
                    { 34, 10.00m, true, 20m, null, "Standard", "Zone 6" },
                    { 35, 18.00m, true, 10m, null, "Express", "Zone 6" },
                    { 36, 24.00m, true, 2m, null, "Registered", "Zone 6" },
                    { 37, 1.50m, false, 0.5m, null, "Standard", "Zone 7" },
                    { 38, 4.50m, false, 0.5m, null, "Express", "Zone 7" },
                    { 39, 6.00m, false, 0.5m, null, "Registered", "Zone 7" },
                    { 40, 10.00m, true, 20m, null, "Standard", "Zone 7" },
                    { 41, 18.00m, true, 10m, null, "Express", "Zone 7" },
                    { 42, 24.00m, true, 2m, null, "Registered", "Zone 7" },
                    { 43, 1.50m, false, 0.5m, null, "Standard", "Zone 8" },
                    { 44, 4.50m, false, 0.5m, null, "Express", "Zone 8" },
                    { 45, 6.00m, false, 0.5m, null, "Registered", "Zone 8" },
                    { 46, 10.00m, true, 20m, null, "Standard", "Zone 8" },
                    { 47, 18.00m, true, 10m, null, "Express", "Zone 8" },
                    { 48, 24.00m, true, 2m, null, "Registered", "Zone 8" },
                    { 49, 1.50m, false, 0.5m, null, "Standard", "Zone 9" },
                    { 50, 4.50m, false, 0.5m, null, "Express", "Zone 9" },
                    { 51, 6.00m, false, 0.5m, null, "Registered", "Zone 9" },
                    { 52, 10.00m, true, 20m, null, "Standard", "Zone 9" },
                    { 53, 18.00m, true, 10m, null, "Express", "Zone 9" },
                    { 54, 24.00m, true, 2m, null, "Registered", "Zone 9" },
                    { 55, 1.50m, false, 0.5m, null, "Standard", "Zone 10" },
                    { 56, 4.50m, false, 0.5m, null, "Express", "Zone 10" },
                    { 57, 6.00m, false, 0.5m, null, "Registered", "Zone 10" },
                    { 58, 10.00m, true, 20m, null, "Standard", "Zone 10" },
                    { 59, 18.00m, true, 10m, null, "Express", "Zone 10" },
                    { 60, 24.00m, true, 2m, null, "Registered", "Zone 10" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStock_Sku",
                table: "BranchStock",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "UQ_BranchStock_Branch_Sku",
                table: "BranchStock",
                columns: new[] { "BranchCode", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchCode_CounterCode",
                table: "CounterTransactions",
                columns: new[] { "BranchCode", "CounterCode" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_ClerkStaffId",
                table: "CounterTransactions",
                column: "ClerkStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_VoidedByStaffId",
                table: "CounterTransactions",
                column: "VoidedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityVerifications_TransactionLineId",
                table: "IdentityVerifications",
                column: "TransactionLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoneyOrders_TransactionLineId",
                table: "MoneyOrders",
                column: "TransactionLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionId",
                table: "Payments",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Name",
                table: "ProductCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShippingRateId",
                table: "Shipments",
                column: "ShippingRateId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TransactionLineId",
                table: "Shipments",
                column: "TransactionLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ShippingRates_Zone_Type_Level",
                table: "ShippingRates",
                columns: new[] { "ZoneId", "IsParcel", "ServiceLevel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShippingZones_Name",
                table: "ShippingZones",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_HomeBranchCode",
                table: "Staff",
                column: "HomeBranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_RoleId",
                table: "Staff",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UQ_Staff_Email",
                table: "Staff",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_BranchCode",
                table: "StockAdjustments",
                column: "BranchCode");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_ChangedBy",
                table: "StockAdjustments",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Sku",
                table: "StockAdjustments",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionLines_Sku",
                table: "TransactionLines",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "UQ_TransactionLines_Transaction_Number",
                table: "TransactionLines",
                columns: new[] { "TransactionId", "LineNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "BranchStock");

            migrationBuilder.DropTable(
                name: "IdentityVerifications");

            migrationBuilder.DropTable(
                name: "MoneyOrders");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "StockAdjustments");

            migrationBuilder.DropTable(
                name: "ShippingRates");

            migrationBuilder.DropTable(
                name: "TransactionLines");

            migrationBuilder.DropTable(
                name: "ShippingZones");

            migrationBuilder.DropTable(
                name: "CounterTransactions");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Counters");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
