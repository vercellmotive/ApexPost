using ApexPost.Domain.Enums;

namespace ApexPost.Application.Queries;

// Read models: reports and screens project straight into these DTOs and never load tracked entities.
// Query and command services share one database; no separate read store exists.
public sealed record DashboardCountsDto(
    IReadOnlyList<string> BranchCodes,
    int ProductCount,
    int TransactionCount,
    IReadOnlyList<string> InactiveBranches,
    IReadOnlyList<LowStockRowDto> LowStock);

public sealed record DailySalesRowDto(string BranchCode, decimal RetailTotal, decimal ServicesTotal);
public sealed record LowStockRowDto(string BranchCode, string Sku, string ProductName, int QuantityOnHand);
public sealed record ShippingVolumeRowDto(string ZoneName, ServiceLevel ServiceLevel, int Shipments);
public sealed record MoneyOrderAuditRowDto(string BranchCode, int MoneyOrders, int IdentityVerifications);

public sealed record CatalogueRowDto(string Sku, string Name, string Category, decimal UnitPrice, int QuantityOnHand);
public sealed record ProductRowDto(string Sku, string Name, string Category, decimal UnitPrice, TaxCategory TaxCategory);
public sealed record BranchRowDto(
    string Code,
    string Name,
    string Region,
    TimeSpan OpenTime,
    TimeSpan CloseTime,
    string TimeZoneId,
    bool IsActive,
    IReadOnlyList<string> CounterTerminals);
public sealed record StaffRowDto(string Id, string Name, string Email, StaffRole Role, string HomeBranchCode, bool IsActive);
public sealed record TransactionRowDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    string CounterId,
    string ClerkStaffId,
    int LineCount,
    decimal GrandTotal,
    string PaymentMethod,
    TransactionStatus Status,
    string? VoidReason);
public sealed record CheckoutStockRowDto(string Sku, string Name, decimal UnitPrice, int QuantityOnHand);
public sealed record CheckoutOptionsDto(
    string BranchCode,
    IReadOnlyList<string> CounterTerminals,
    IReadOnlyList<CheckoutStockRowDto> Stock);

public interface IDashboardQueries
{
    Task<DashboardCountsDto> GetCountsAsync(string? branchCode, int lowStockThreshold, CancellationToken ct = default);
}

public interface IReportQueries
{
    Task<IReadOnlyList<DailySalesRowDto>> GetDailyBranchSalesAsync(string? branchCode, DateOnly date, CancellationToken ct = default);
    Task<DateOnly?> GetLatestSalesDateAsync(string? branchCode, CancellationToken ct = default);
    Task<IReadOnlyList<LowStockRowDto>> GetLowStockAsync(string? branchCode, int threshold, CancellationToken ct = default);
    Task<IReadOnlyList<ShippingVolumeRowDto>> GetShippingVolumeAsync(string? branchCode, CancellationToken ct = default);
    Task<IReadOnlyList<MoneyOrderAuditRowDto>> GetMoneyOrderAuditAsync(string? branchCode, CancellationToken ct = default);
}

public interface IScreenQueries
{
    Task<IReadOnlyList<CatalogueRowDto>> GetBranchCatalogueAsync(string branchCode, CancellationToken ct = default);
    Task<IReadOnlyList<ProductRowDto>> GetProductsAsync(CancellationToken ct = default);
    Task<ProductRowDto?> GetProductAsync(string sku, CancellationToken ct = default);
    Task<IReadOnlyList<BranchRowDto>> GetBranchesAsync(CancellationToken ct = default);
    Task<BranchRowDto?> GetBranchAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<StaffRowDto>> GetStaffAsync(CancellationToken ct = default);
    Task<StaffRowDto?> GetStaffMemberAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionRowDto>> GetBranchTransactionsAsync(string branchCode, int take, CancellationToken ct = default);
    Task<CheckoutOptionsDto?> GetCheckoutOptionsAsync(string branchCode, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
}
