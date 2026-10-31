using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApexPost.Infrastructure.Persistence;

public static class AuditColumns
{
    public const string CreatedAtUtc = nameof(CreatedAtUtc);
    public const string CreatedBy = nameof(CreatedBy);
    public const string ModifiedAtUtc = nameof(ModifiedAtUtc);
    public const string RowVersion = nameof(RowVersion);

    public static void AddTo<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<DateTime>(CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property<string>(CreatedBy).HasColumnType("varchar(50)").HasDefaultValue("system");
        builder.Property<DateTime?>(ModifiedAtUtc);
    }
}

public static class ValueConverters
{
    public static readonly ValueConverter<BranchCode, string> BranchCode = new(code => code.Value, value => new BranchCode(value));
    public static readonly ValueConverter<Sku, string> Sku = new(sku => sku.Value, value => new Sku(value));
    public static readonly ValueConverter<Money, decimal> Money = new(money => money.Amount, value => new Money(value, "AUD"));
    public static readonly ValueConverter<Weight, decimal> Weight = new(weight => weight.Kilograms, value => ApexPost.Domain.ValueObjects.Weight.FromKilograms(value));
}

// Stable reference-data keys, shared by HasData and the mappers so writes need no lookup query.
public static class ReferenceData
{
    public const int ZoneCount = 10;

    public static readonly IReadOnlyDictionary<string, int> CategoryIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [ProductCategories.Packaging] = 1,
        [ProductCategories.Stationery] = 2,
        [ProductCategories.OfficeSupplies] = 3
    };

    public static readonly decimal[] ZoneMultipliers = [1.0m, 1.0m, 1.25m, 1.25m, 1.5m, 1.5m, 1.75m, 1.75m, 2.0m, 2.0m];

    public static int ShippingRateId(string zoneName, bool isParcel, ServiceLevel serviceLevel)
    {
        if (!zoneName.StartsWith("Zone ", StringComparison.Ordinal)
            || !int.TryParse(zoneName.AsSpan(5), out var zoneNumber)
            || zoneNumber is < 1 or > ZoneCount)
        {
            throw new ArgumentOutOfRangeException(nameof(zoneName), $"Unknown shipping zone '{zoneName}'.");
        }

        return ((zoneNumber - 1) * 6) + (isParcel ? 3 : 0) + (int)serviceLevel + 1;
    }

    public static IEnumerable<ShippingRateRecord> ShippingRates()
    {
        foreach (var zoneNumber in Enumerable.Range(1, ZoneCount))
        {
            foreach (var isParcel in new[] { false, true })
            {
                foreach (var level in Enum.GetValues<ServiceLevel>())
                {
                    var zoneName = $"Zone {zoneNumber}";
                    yield return new ShippingRateRecord
                    {
                        Id = ShippingRateId(zoneName, isParcel, level),
                        ZoneId = zoneName,
                        IsParcel = isParcel,
                        ServiceLevel = level,
                        BaseRate = (isParcel, level) switch
                        {
                            (false, ServiceLevel.Standard) => 1.50m,
                            (false, ServiceLevel.Express) => 4.50m,
                            (false, ServiceLevel.Registered) => 6.00m,
                            (true, ServiceLevel.Standard) => 10.00m,
                            (true, ServiceLevel.Express) => 18.00m,
                            _ => 24.00m
                        },
                        MaxWeightKg = (isParcel, level) switch
                        {
                            (false, _) => 0.5m,
                            (true, ServiceLevel.Standard) => 20m,
                            (true, ServiceLevel.Express) => 10m,
                            _ => 2m
                        }
                    };
                }
            }
        }
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<RoleRecord>
{
    public void Configure(EntityTypeBuilder<RoleRecord> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasConversion<int>().ValueGeneratedNever();
        builder.Property(role => role.Name).HasColumnType("varchar(30)").IsRequired();
        builder.HasIndex(role => role.Name).IsUnique();
        builder.HasData(Enum.GetValues<StaffRole>().Select(role => new RoleRecord { Id = role, Name = role.ToString() }));
        AuditColumns.AddTo(builder);
    }
}

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategoryRecord>
{
    public void Configure(EntityTypeBuilder<ProductCategoryRecord> builder)
    {
        builder.ToTable("ProductCategories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Name).HasColumnType("nvarchar(50)").IsRequired();
        builder.HasIndex(category => category.Name).IsUnique();
        builder.HasData(ReferenceData.CategoryIds.Select(pair => new ProductCategoryRecord { Id = pair.Value, Name = pair.Key }));
        AuditColumns.AddTo(builder);
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<BranchRecord>
{
    public void Configure(EntityTypeBuilder<BranchRecord> builder)
    {
        builder.ToTable("Branches", table =>
            table.HasCheckConstraint("CK_Branches_Hours", "[OpenTime] < [CloseTime]"));
        builder.HasKey(branch => branch.Id);
        builder.Property(branch => branch.Code).HasConversion(ValueConverters.BranchCode).HasColumnType("varchar(10)");
        builder.HasAlternateKey(branch => branch.Code).HasName("UQ_Branches_BranchCode");
        builder.Property(branch => branch.Code).HasColumnName("BranchCode");
        builder.Property(branch => branch.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(branch => branch.Region).HasColumnType("nvarchar(50)").IsRequired();
        builder.Property(branch => branch.TimeZoneId).HasColumnType("varchar(64)").IsRequired();
        // The sentinel makes EF Core send an explicit false rather than treating it as "use the default".
        builder.Property(branch => branch.IsActive).HasDefaultValue(true).HasSentinel(true);
        builder.HasMany(branch => branch.Counters)
            .WithOne()
            .HasForeignKey(counter => counter.BranchCode)
            .HasPrincipalKey(branch => branch.Code)
            .OnDelete(DeleteBehavior.Restrict);
        AuditColumns.AddTo(builder);
    }
}

internal sealed class CounterConfiguration : IEntityTypeConfiguration<CounterRecord>
{
    public void Configure(EntityTypeBuilder<CounterRecord> builder)
    {
        builder.ToTable("Counters");
        builder.HasKey(counter => counter.Id);
        builder.Property(counter => counter.BranchCode).HasConversion(ValueConverters.BranchCode).HasColumnType("varchar(10)");
        builder.Property(counter => counter.TerminalCode).HasColumnType("varchar(10)").IsRequired();
        builder.HasAlternateKey(counter => new { counter.BranchCode, counter.TerminalCode }).HasName("UQ_Counters_Branch_Terminal");
        AuditColumns.AddTo(builder);
    }
}

// Staff maps the rich domain entity directly. Audit columns stay shadow properties, so the domain never sees them.
internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("Staff");
        builder.HasKey(staff => staff.Id);
        builder.Property(staff => staff.Id).HasColumnType("varchar(20)").ValueGeneratedNever();
        builder.Property(staff => staff.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(staff => staff.Email).HasColumnType("varchar(254)").IsRequired();
        builder.HasIndex(staff => staff.Email).IsUnique().HasDatabaseName("UQ_Staff_Email");
        builder.Property(staff => staff.Role).HasConversion<int>().HasColumnName("RoleId");
        builder.Property(staff => staff.HomeBranchCode).HasConversion(ValueConverters.BranchCode).HasColumnType("varchar(10)");
        builder.Property(staff => staff.IsActive).HasDefaultValue(true).HasSentinel(true);
        builder.Property(staff => staff.PasswordHash).HasColumnType("varchar(200)");
        builder.HasOne<RoleRecord>().WithMany().HasForeignKey(staff => staff.Role).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BranchRecord>().WithMany()
            .HasForeignKey(staff => staff.HomeBranchCode)
            .HasPrincipalKey(branch => branch.Code)
            .OnDelete(DeleteBehavior.Restrict);
        AuditColumns.AddTo(builder);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<ProductRecord>
{
    public void Configure(EntityTypeBuilder<ProductRecord> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_UnitPrice", "[UnitPrice] > 0");
            table.HasCheckConstraint("CK_Products_TaxCategory", "[TaxCategory] IN ('Exempt','StandardGst','Reduced')");
        });
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Sku).HasConversion(ValueConverters.Sku).HasColumnType("varchar(20)");
        builder.HasAlternateKey(product => product.Sku).HasName("UQ_Products_Sku");
        builder.Property(product => product.Name).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(product => product.TaxCategory).HasConversion<string>().HasColumnType("varchar(20)");
        builder.HasOne(product => product.Category).WithMany().HasForeignKey(product => product.CategoryId).OnDelete(DeleteBehavior.Restrict);
        AuditColumns.AddTo(builder);
    }
}

// BranchStock maps the rich domain entity directly, so its index is Fluent: [Index] would make Domain depend on EF Core.
internal sealed class BranchStockConfiguration : IEntityTypeConfiguration<BranchStock>
{
    public void Configure(EntityTypeBuilder<BranchStock> builder)
    {
        builder.ToTable("BranchStock", table =>
            table.HasCheckConstraint("CK_BranchStock_Quantity", "[QuantityOnHand] >= 0"));
        builder.HasKey(stock => stock.Id);
        builder.Property(stock => stock.Id).ValueGeneratedNever();
        builder.Property(stock => stock.BranchCode).HasConversion(ValueConverters.BranchCode).HasColumnType("varchar(10)");
        builder.Property(stock => stock.ProductSku).HasConversion(ValueConverters.Sku).HasColumnType("varchar(20)").HasColumnName("Sku");
        builder.Property(stock => stock.QuantityOnHand).HasDefaultValue(0);
        builder.Ignore(stock => stock.RestockedReversalTransactionIds);
        builder.HasIndex(stock => new { stock.BranchCode, stock.ProductSku }).IsUnique().HasDatabaseName("UQ_BranchStock_Branch_Sku");
        builder.HasOne<BranchRecord>().WithMany().HasForeignKey(stock => stock.BranchCode).HasPrincipalKey(branch => branch.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductRecord>().WithMany().HasForeignKey(stock => stock.ProductSku).HasPrincipalKey(product => product.Sku).OnDelete(DeleteBehavior.Restrict);
        // Filtered, because measurements showed a QuantityOnHand key made bulk stock updates about 6x slower (ADR-35).
        builder.HasIndex(stock => new { stock.BranchCode, stock.ProductSku }, IndexNames.LowStock)
            .HasFilter("[QuantityOnHand] <= 5")
            .IncludeProperties(stock => stock.QuantityOnHand);
        builder.Property<byte[]>(AuditColumns.RowVersion).IsRowVersion().IsRequired();
        AuditColumns.AddTo(builder);
    }
}

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustmentRecord>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentRecord> builder)
    {
        builder.ToTable("StockAdjustments", table =>
        {
            table.HasCheckConstraint("CK_StockAdjustments_Delta", "[Delta] <> 0");
            table.HasCheckConstraint("CK_StockAdjustments_QuantityAfter", "[QuantityAfter] >= 0");
            table.HasCheckConstraint("CK_StockAdjustments_Reason", "[Reason] IN ('Replenishment','Damage','CountCorrection','CustomerReturn')");
        });
        builder.HasKey(adjustment => adjustment.Id);
        builder.Property(adjustment => adjustment.BranchCode).HasConversion(ValueConverters.BranchCode).HasColumnType("varchar(10)");
        builder.Property(adjustment => adjustment.Sku).HasConversion(ValueConverters.Sku).HasColumnType("varchar(20)");
        builder.Property(adjustment => adjustment.Reason).HasColumnType("varchar(30)").IsRequired();
        builder.Property(adjustment => adjustment.ChangedBy).HasColumnType("varchar(20)").IsRequired();
        builder.Property(adjustment => adjustment.ChangedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne<BranchRecord>().WithMany().HasForeignKey(adjustment => adjustment.BranchCode).HasPrincipalKey(branch => branch.Code).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductRecord>().WithMany().HasForeignKey(adjustment => adjustment.Sku).HasPrincipalKey(product => product.Sku).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Staff>().WithMany().HasForeignKey(adjustment => adjustment.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        AuditColumns.AddTo(builder);
    }
}

internal sealed class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZone>
{
    public void Configure(EntityTypeBuilder<ShippingZone> builder)
    {
        builder.ToTable("ShippingZones", table =>
            table.HasCheckConstraint("CK_ShippingZones_Multiplier", "[Multiplier] > 0"));
        builder.HasKey(zone => zone.Id);
        builder.Property(zone => zone.Id).HasColumnType("varchar(20)").ValueGeneratedNever();
        builder.Property(zone => zone.Name).HasColumnType("nvarchar(50)").IsRequired();
        builder.Property(zone => zone.Multiplier).HasPrecision(6, 3);
        builder.HasIndex(zone => zone.Name).IsUnique();
        builder.HasData(ReferenceData.ZoneMultipliers.Select((multiplier, index) => new ShippingZone
        {
            Id = $"Zone {index + 1}",
            Name = $"Zone {index + 1}",
            Multiplier = multiplier
        }));
        AuditColumns.AddTo(builder);
    }
}

internal sealed class ShippingRateConfiguration : IEntityTypeConfiguration<ShippingRateRecord>
{
    public void Configure(EntityTypeBuilder<ShippingRateRecord> builder)
    {
        builder.ToTable("ShippingRates", table =>
        {
            table.HasCheckConstraint("CK_ShippingRates_BaseRate", "[BaseRate] > 0");
            table.HasCheckConstraint("CK_ShippingRates_MaxWeight", "[MaxWeightKg] > 0 AND [MaxWeightKg] <= 20");
            table.HasCheckConstraint("CK_ShippingRates_ServiceLevel", "[ServiceLevel] IN ('Standard','Express','Registered')");
        });
        builder.HasKey(rate => rate.Id);
        builder.Property(rate => rate.Id).ValueGeneratedNever();
        builder.Property(rate => rate.ZoneId).HasColumnType("varchar(20)").IsRequired();
        builder.Property(rate => rate.ServiceLevel).HasConversion<string>().HasColumnType("varchar(20)");
        builder.Property(rate => rate.MaxWeightKg).HasPrecision(9, 3);
        builder.HasIndex(rate => new { rate.ZoneId, rate.IsParcel, rate.ServiceLevel }).IsUnique().HasDatabaseName("UQ_ShippingRates_Zone_Type_Level");
        builder.HasOne<ShippingZone>().WithMany().HasForeignKey(rate => rate.ZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(ReferenceData.ShippingRates());
        AuditColumns.AddTo(builder);
    }
}

internal sealed class CounterTransactionConfiguration : IEntityTypeConfiguration<CounterTransactionRecord>
{
    public void Configure(EntityTypeBuilder<CounterTransactionRecord> builder)
    {
        builder.ToTable("CounterTransactions", table =>
            table.HasCheckConstraint("CK_CounterTransactions_Status", "[Status] IN ('Open','Paid','Voided','Refunded')"));
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id).ValueGeneratedNever();
        builder.Property(transaction => transaction.BranchCode).HasConversion(ValueConverters.BranchCode).HasColumnType("varchar(10)");
        builder.Property(transaction => transaction.CounterCode).HasColumnType("varchar(10)").IsRequired();
        builder.Property(transaction => transaction.ClerkStaffId).HasColumnType("varchar(20)").IsRequired();
        builder.Property(transaction => transaction.VoidedByStaffId).HasColumnType("varchar(20)");
        builder.Property(transaction => transaction.Status).HasConversion<string>().HasColumnType("varchar(20)").HasDefaultValue(TransactionStatus.Open).HasSentinel((TransactionStatus)(-1));
        builder.Property(transaction => transaction.OccurredAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(transaction => transaction.BusinessDate).HasColumnType("date");
        builder.Property(transaction => transaction.VoidReason).HasColumnType("nvarchar(200)");

        // The key columns are declared by [Index] on the record; [Index] has no INCLUDE option.
        builder.HasIndex(transaction => new { transaction.BranchCode, transaction.BusinessDate }, IndexNames.TransactionByBranchAndDate)
            .IncludeProperties(transaction => transaction.Status);

        // The composite key makes a counter from another branch impossible to record.
        builder.HasOne<CounterRecord>().WithMany()
            .HasForeignKey(transaction => new { transaction.BranchCode, transaction.CounterCode })
            .HasPrincipalKey(counter => new { counter.BranchCode, counter.TerminalCode })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Staff>().WithMany().HasForeignKey(transaction => transaction.ClerkStaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Staff>().WithMany().HasForeignKey(transaction => transaction.VoidedByStaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(transaction => transaction.Lines).WithOne(line => line.Transaction).HasForeignKey(line => line.TransactionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(transaction => transaction.Payments).WithOne().HasForeignKey(payment => payment.TransactionId).OnDelete(DeleteBehavior.Cascade);
        builder.Property<byte[]>(AuditColumns.RowVersion).IsRowVersion().IsRequired();
        AuditColumns.AddTo(builder);
    }
}

internal sealed class TransactionLineConfiguration : IEntityTypeConfiguration<TransactionLineRecord>
{
    public void Configure(EntityTypeBuilder<TransactionLineRecord> builder)
    {
        builder.ToTable("TransactionLines", table =>
        {
            table.HasCheckConstraint("CK_TransactionLines_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_TransactionLines_Amounts", "[UnitPrice] >= 0 AND [TaxAmount] >= 0 AND [LineTotal] >= 0");
            table.HasCheckConstraint("CK_TransactionLines_Type", "[LineType] IN ('RetailProduct','ParcelShipping','MoneyOrder','IdentityVerification')");
            table.HasCheckConstraint("CK_TransactionLines_RetailSku", "([LineType] = 'RetailProduct' AND [Sku] IS NOT NULL) OR ([LineType] <> 'RetailProduct' AND [Sku] IS NULL)");
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.Description).HasColumnType("nvarchar(200)").IsRequired();
        builder.Property(line => line.LineType).HasConversion<string>().HasColumnType("varchar(30)");
        builder.Property(line => line.UnitPrice).HasConversion(ValueConverters.Money).HasColumnType("decimal(18,2)");
        builder.Property(line => line.TaxAmount).HasConversion(ValueConverters.Money).HasColumnType("decimal(18,2)");
        builder.Property(line => line.TaxCategory).HasConversion<string>().HasColumnType("varchar(20)");
        builder.Property(line => line.Sku).HasConversion(ValueConverters.Sku).HasColumnType("varchar(20)");
        builder.HasIndex(line => new { line.TransactionId, line.LineNumber }).IsUnique().HasDatabaseName("UQ_TransactionLines_Transaction_Number");
        builder.HasOne<ProductRecord>().WithMany().HasForeignKey(line => line.Sku).HasPrincipalKey(product => product.Sku).OnDelete(DeleteBehavior.Restrict);
        AuditColumns.AddTo(builder);
    }
}

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<ShipmentRecord>
{
    public void Configure(EntityTypeBuilder<ShipmentRecord> builder)
    {
        builder.ToTable("Shipments", table =>
            table.HasCheckConstraint("CK_Shipments_Weight", "[WeightKg] > 0 AND [WeightKg] <= 20"));
        builder.HasKey(shipment => shipment.Id);
        builder.Property(shipment => shipment.Id).ValueGeneratedNever();
        builder.Property(shipment => shipment.Weight).HasConversion(ValueConverters.Weight).HasColumnName("WeightKg").HasColumnType("decimal(9,3)");
        builder.OwnsOne(shipment => shipment.Dimensions, dimensions =>
        {
            dimensions.Property(size => size.LengthCm).HasColumnName("LengthCm").HasPrecision(9, 2);
            dimensions.Property(size => size.WidthCm).HasColumnName("WidthCm").HasPrecision(9, 2);
            dimensions.Property(size => size.HeightCm).HasColumnName("HeightCm").HasPrecision(9, 2);
        });
        builder.HasIndex(shipment => shipment.ShippingRateId, IndexNames.ShipmentByRate)
            .IncludeProperties(shipment => shipment.TransactionLineId);
        builder.HasOne(shipment => shipment.Line).WithOne(line => line.Shipment).HasForeignKey<ShipmentRecord>(shipment => shipment.TransactionLineId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(shipment => shipment.Rate).WithMany().HasForeignKey(shipment => shipment.ShippingRateId).OnDelete(DeleteBehavior.Restrict);
        AuditColumns.AddTo(builder);
    }
}

internal sealed class MoneyOrderConfiguration : IEntityTypeConfiguration<MoneyOrderRecord>
{
    public void Configure(EntityTypeBuilder<MoneyOrderRecord> builder)
    {
        builder.ToTable("MoneyOrders", table =>
        {
            table.HasCheckConstraint("CK_MoneyOrders_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_MoneyOrders_Fee", "[Fee] >= 0");
        });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.Amount).HasConversion(ValueConverters.Money).HasColumnType("decimal(18,2)");
        builder.Property(order => order.Fee).HasConversion(ValueConverters.Money).HasColumnType("decimal(18,2)");
        builder.Property(order => order.Sender).HasColumnType("nvarchar(100)").IsRequired();
        builder.Property(order => order.Recipient).HasColumnType("nvarchar(100)").IsRequired();
        // A filtered index stays small because auditors only search high-value orders; [Index] has no filter option.
        builder.HasIndex(order => order.Amount, IndexNames.HighValueMoneyOrder)
            .HasFilter("[Amount] > 1000")
            .IncludeProperties(order => new { order.Fee, order.TransactionLineId });
        builder.HasOne(order => order.Line).WithOne(line => line.MoneyOrder).HasForeignKey<MoneyOrderRecord>(order => order.TransactionLineId).OnDelete(DeleteBehavior.Cascade);
        AuditColumns.AddTo(builder);
    }
}

internal sealed class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerificationRecord>
{
    public void Configure(EntityTypeBuilder<IdentityVerificationRecord> builder)
    {
        builder.ToTable("IdentityVerifications", table =>
        {
            table.HasCheckConstraint("CK_IdentityVerifications_Masked", "[MaskedDocumentNumber] LIKE '******____'");
            table.HasCheckConstraint("CK_IdentityVerifications_DocumentType", "[DocumentType] IN ('Passport','DriversLicense','NationalIdentityCard')");
        });
        builder.HasKey(verification => verification.Id);
        builder.Property(verification => verification.Id).ValueGeneratedNever();
        builder.Property(verification => verification.DocumentType).HasConversion<string>().HasColumnType("varchar(30)");
        builder.Property(verification => verification.MaskedDocumentNumber).HasColumnType("varchar(10)").IsRequired();
        builder.HasOne(verification => verification.Line).WithOne(line => line.IdentityVerification).HasForeignKey<IdentityVerificationRecord>(verification => verification.TransactionLineId).OnDelete(DeleteBehavior.Cascade);
        AuditColumns.AddTo(builder);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
            table.HasCheckConstraint("CK_Payments_Kind", "[Kind] IN ('Sale','Void','Refund')");
            table.HasCheckConstraint("CK_Payments_Method", "[Method] IN ('Cash','Card','EFTPOS')");
        });
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.Kind).HasColumnType("varchar(10)").IsRequired().HasDefaultValue(PaymentKinds.Sale);
        builder.Property(payment => payment.Method).HasColumnType("varchar(10)").IsRequired();
        builder.Property(payment => payment.Amount).HasConversion(ValueConverters.Money).HasColumnType("decimal(18,2)");
        builder.Property(payment => payment.PaidAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        AuditColumns.AddTo(builder);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntryRecord>
{
    public void Configure(EntityTypeBuilder<AuditEntryRecord> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.CorrelationId).HasColumnType("varchar(128)").IsRequired();
        builder.Property(entry => entry.StaffId).HasColumnType("varchar(20)").IsRequired();
        builder.Property(entry => entry.Role).HasColumnType("varchar(30)").IsRequired();
        builder.Property(entry => entry.BranchCode).HasColumnType("varchar(10)").IsRequired();
        builder.Property(entry => entry.Controller).HasColumnType("varchar(100)").IsRequired();
        builder.Property(entry => entry.Action).HasColumnType("varchar(100)").IsRequired();
        builder.Property(entry => entry.Parameters).HasColumnType("nvarchar(2000)").IsRequired();
        builder.Property(entry => entry.TimestampUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        AuditColumns.AddTo(builder);
    }
}
