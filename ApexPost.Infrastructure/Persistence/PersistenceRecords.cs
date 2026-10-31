using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

// Persistence models for aggregates whose normalized tables differ from the domain shape (see ADR-29).
// [Index] states an index beside the columns it covers. Options the attribute cannot express,
// such as INCLUDE columns and filters, are added to the same index in the Fluent configuration.
public static class IndexNames
{
    public const string StockAdjustmentHistory = "IX_StockAdjustments_Branch_Sku_Time";
    public const string RateByZoneAndLevel = "IX_ShippingRates_Zone_Level";
    public const string TransactionByBranchAndDate = "IX_CounterTransactions_Branch_Date";
    public const string ShipmentByRate = "IX_Shipments_Rate";
    public const string HighValueMoneyOrder = "IX_MoneyOrders_HighValue";
    public const string LowStock = "IX_BranchStock_LowStock";
}

public sealed class RoleRecord
{
    public StaffRole Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ProductCategoryRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class BranchRecord
{
    public int Id { get; set; }
    public BranchCode Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<CounterRecord> Counters { get; set; } = [];
}

public sealed class CounterRecord
{
    public int Id { get; set; }
    public BranchCode BranchCode { get; set; }
    public string TerminalCode { get; set; } = string.Empty;
}

public sealed class ProductRecord
{
    public int Id { get; set; }
    public Sku Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public ProductCategoryRecord? Category { get; set; }
    public decimal UnitPrice { get; set; }
    public TaxCategory TaxCategory { get; set; }
}

[Index(nameof(BranchCode), nameof(Sku), nameof(ChangedAtUtc), Name = IndexNames.StockAdjustmentHistory)]
public sealed class StockAdjustmentRecord
{
    public long Id { get; set; }
    public BranchCode BranchCode { get; set; }
    public Sku Sku { get; set; }
    public int Delta { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}

[Index(nameof(ZoneId), nameof(ServiceLevel), Name = IndexNames.RateByZoneAndLevel)]
public sealed class ShippingRateRecord
{
    public int Id { get; set; }
    public string ZoneId { get; set; } = string.Empty;
    public bool IsParcel { get; set; }
    public ServiceLevel ServiceLevel { get; set; }
    public decimal BaseRate { get; set; }
    public decimal MaxWeightKg { get; set; }
}

[Index(nameof(BranchCode), nameof(BusinessDate), Name = IndexNames.TransactionByBranchAndDate)]
public sealed class CounterTransactionRecord
{
    public Guid Id { get; set; }
    public BranchCode BranchCode { get; set; }
    public string CounterCode { get; set; } = string.Empty;
    public string ClerkStaffId { get; set; } = string.Empty;
    public TransactionStatus Status { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string? VoidReason { get; set; }
    public string? VoidedByStaffId { get; set; }
    public bool InventoryRestocked { get; set; }
    public List<TransactionLineRecord> Lines { get; set; } = [];
    public List<PaymentRecord> Payments { get; set; } = [];
}

public sealed class TransactionLineRecord
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public CounterTransactionRecord? Transaction { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public LineItemType LineType { get; set; }
    public int Quantity { get; set; }
    public Money UnitPrice { get; set; }
    public TaxCategory TaxCategory { get; set; }
    public Money TaxAmount { get; set; }

    // Gross line value frozen at sale time, so later tax changes never rewrite history.
    public decimal LineTotal { get; set; }
    public Sku? Sku { get; set; }
    public ShipmentRecord? Shipment { get; set; }
    public MoneyOrderRecord? MoneyOrder { get; set; }
    public IdentityVerificationRecord? IdentityVerification { get; set; }
}

// Dimensions is a value object with no identity of its own, so it is an owned type.
public sealed class ParcelDimensions
{
    public decimal LengthCm { get; set; }
    public decimal WidthCm { get; set; }
    public decimal HeightCm { get; set; }
}

[Index(nameof(ShippingRateId), Name = IndexNames.ShipmentByRate)]
public sealed class ShipmentRecord
{
    public Guid Id { get; set; }
    public Guid TransactionLineId { get; set; }
    public TransactionLineRecord? Line { get; set; }
    public int ShippingRateId { get; set; }
    public ShippingRateRecord? Rate { get; set; }
    public Weight Weight { get; set; }
    public ParcelDimensions Dimensions { get; set; } = new();
}

[Index(nameof(Amount), Name = IndexNames.HighValueMoneyOrder)]
public sealed class MoneyOrderRecord
{
    public Guid Id { get; set; }
    public Guid TransactionLineId { get; set; }
    public TransactionLineRecord? Line { get; set; }
    public Money Amount { get; set; }
    public Money Fee { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
}

public sealed class IdentityVerificationRecord
{
    public Guid Id { get; set; }
    public Guid TransactionLineId { get; set; }
    public TransactionLineRecord? Line { get; set; }
    public DocumentType DocumentType { get; set; }

    // Masked only. The column length and CHECK constraint make a full number unstorable.
    public string MaskedDocumentNumber { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
}

public sealed class PaymentRecord
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public string Kind { get; set; } = PaymentKinds.Sale;
    public string Method { get; set; } = string.Empty;
    public Money Amount { get; set; }
    public DateTime PaidAtUtc { get; set; }
}

public static class PaymentKinds
{
    public const string Sale = "Sale";
    public const string Void = "Void";
    public const string Refund = "Refund";
}

[Index(nameof(StaffId), nameof(TimestampUtc), Name = "IX_AuditEntries_Staff_Time")]
[Index(nameof(BranchCode), nameof(TimestampUtc), Name = "IX_AuditEntries_Branch_Time")]
public sealed class AuditEntryRecord
{
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public DateTime TimestampUtc { get; set; }
}
