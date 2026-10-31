using System.Data;
using ApexPost.Application.Queries;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

// Query shapes live here so tests can capture their SQL with ToQueryString(); IQueryable stays inside Infrastructure.
public static class QueryShapes
{
    public static IQueryable<BranchRecord> TrackedBranch(ApexPostDbContext db, BranchCode code) =>
        db.Branches.Include(branch => branch.Counters).Where(branch => branch.Code == code);

    public static IQueryable<ProductRecord> TrackedProduct(ApexPostDbContext db, Sku sku) =>
        db.Products.Include(product => product.Category).Where(product => product.Sku == sku);

    public static IQueryable<BranchStock> TrackedStock(ApexPostDbContext db, BranchCode branchCode, IReadOnlyCollection<Sku> skus) =>
        db.BranchStock.Where(stock => stock.BranchCode == branchCode && skus.Contains(stock.ProductSku));

    // Split queries stop the one-to-many lines and payments from multiplying rows.
    public static IQueryable<CounterTransactionRecord> TrackedTransaction(ApexPostDbContext db, Guid id) =>
        db.CounterTransactions
            .Include(transaction => transaction.Payments)
            .Include(transaction => transaction.Lines).ThenInclude(line => line.Shipment!).ThenInclude(shipment => shipment.Rate)
            .Include(transaction => transaction.Lines).ThenInclude(line => line.MoneyOrder)
            .Include(transaction => transaction.Lines).ThenInclude(line => line.IdentityVerification)
            .AsSplitQuery()
            .Where(transaction => transaction.Id == id);

    public static IQueryable<DashboardBranchShape> DashboardBranches(ApexPostDbContext db, string? branchCode)
    {
        var code = new BranchCode(branchCode ?? string.Empty);
        return db.Branches.AsNoTracking()
            .Where(branch => branchCode == null || branch.Code == code)
            .OrderBy(branch => branch.Code)
            .Select(branch => new DashboardBranchShape(branch.Code, branch.IsActive));
    }

    public static IQueryable<CounterTransactionRecord> BranchTransactionCount(ApexPostDbContext db, string? branchCode)
    {
        var code = new BranchCode(branchCode ?? string.Empty);
        return db.CounterTransactions.Where(transaction => branchCode == null || transaction.BranchCode == code);
    }

    public static IQueryable<LowStockShape> LowStock(ApexPostDbContext db, string? branchCode, int threshold)
    {
        var code = new BranchCode(branchCode ?? string.Empty);
        return from stock in db.BranchStock.AsNoTracking()
               join product in db.Products on stock.ProductSku equals product.Sku
               where stock.QuantityOnHand <= threshold && (branchCode == null || stock.BranchCode == code)
               orderby stock.BranchCode, stock.QuantityOnHand
               select new LowStockShape(stock.BranchCode, stock.ProductSku, product.Name, stock.QuantityOnHand);
    }

    public static IQueryable<ShippingVolumeShape> ShippingVolume(ApexPostDbContext db, string? branchCode)
    {
        var code = new BranchCode(branchCode ?? string.Empty);
        return from shipment in db.Shipments.AsNoTracking()
               join line in db.TransactionLines on shipment.TransactionLineId equals line.Id
               join transaction in db.CounterTransactions on line.TransactionId equals transaction.Id
               join rate in db.ShippingRates on shipment.ShippingRateId equals rate.Id
               where transaction.Status == TransactionStatus.Paid && (branchCode == null || transaction.BranchCode == code)
               group line by new { rate.ZoneId, rate.ServiceLevel } into grouped
               orderby grouped.Key.ZoneId, grouped.Key.ServiceLevel
               select new ShippingVolumeShape(grouped.Key.ZoneId, grouped.Key.ServiceLevel, grouped.Sum(line => line.Quantity));
    }

    public static IQueryable<LineTypeCountShape> MoneyOrderAudit(ApexPostDbContext db, string? branchCode)
    {
        var code = new BranchCode(branchCode ?? string.Empty);
        return from line in db.TransactionLines.AsNoTracking()
               join transaction in db.CounterTransactions on line.TransactionId equals transaction.Id
               where (line.LineType == LineItemType.MoneyOrder || line.LineType == LineItemType.IdentityVerification)
                   && (branchCode == null || transaction.BranchCode == code)
               group line by new { transaction.BranchCode, line.LineType } into grouped
               select new LineTypeCountShape(grouped.Key.BranchCode, grouped.Key.LineType, grouped.Count());
    }

    public static IQueryable<TransactionShape> BranchTransactions(ApexPostDbContext db, string branchCode, int take)
    {
        var code = new BranchCode(branchCode);
        return db.CounterTransactions.AsNoTracking()
            .Where(transaction => transaction.BranchCode == code)
            .OrderByDescending(transaction => transaction.OccurredAtUtc)
            .Take(take)
            .Select(transaction => new TransactionShape(
                transaction.Id,
                transaction.OccurredAtUtc,
                transaction.CounterCode,
                transaction.ClerkStaffId,
                transaction.Lines.Count,
                transaction.Lines.Sum(line => (decimal?)line.LineTotal) ?? 0m,
                transaction.Payments.Where(payment => payment.Kind == PaymentKinds.Sale).Select(payment => payment.Method).FirstOrDefault(),
                transaction.Status,
                transaction.VoidReason));
    }

    public static IQueryable<CatalogueShape> BranchCatalogue(ApexPostDbContext db, string branchCode)
    {
        var code = new BranchCode(branchCode);
        return db.Products.AsNoTracking()
            .OrderBy(product => product.Sku)
            .Select(product => new CatalogueShape(
                product.Sku,
                product.Name,
                product.Category!.Name,
                product.UnitPrice,
                db.BranchStock.Where(stock => stock.BranchCode == code && stock.ProductSku == product.Sku)
                    .Select(stock => (int?)stock.QuantityOnHand)
                    .FirstOrDefault() ?? 0));
    }

    public static IQueryable<DateOnly> LatestSalesDate(ApexPostDbContext db, string? branchCode)
    {
        var code = new BranchCode(branchCode ?? string.Empty);
        return db.CounterTransactions.AsNoTracking()
            .Where(transaction => transaction.Status == TransactionStatus.Paid && (branchCode == null || transaction.BranchCode == code))
            .OrderByDescending(transaction => transaction.BusinessDate)
            .Select(transaction => transaction.BusinessDate);
    }

    public static IQueryable<ProductShape> Products(ApexPostDbContext db, string? sku)
    {
        var key = new Sku(sku ?? string.Empty);
        return db.Products.AsNoTracking()
            .Where(product => sku == null || product.Sku == key)
            .OrderBy(product => product.Sku)
            .Select(product => new ProductShape(product.Sku, product.Name, product.Category!.Name, product.UnitPrice, product.TaxCategory));
    }

    public static IQueryable<BranchShape> Branches(ApexPostDbContext db, string? code)
    {
        var key = new BranchCode(code ?? string.Empty);
        return db.Branches.AsNoTracking()
            .Where(branch => code == null || branch.Code == key)
            .OrderBy(branch => branch.Code)
            .Select(branch => new BranchShape(
                branch.Code,
                branch.Name,
                branch.Region,
                branch.OpenTime,
                branch.CloseTime,
                branch.TimeZoneId,
                branch.IsActive,
                branch.Counters.OrderBy(counter => counter.TerminalCode).Select(counter => counter.TerminalCode).ToList()));
    }

    public static IQueryable<StaffShape> Staff(ApexPostDbContext db, string? id) =>
        db.Staff.AsNoTracking()
            .Where(staff => id == null || staff.Id == id)
            .OrderBy(staff => staff.HomeBranchCode)
            .ThenBy(staff => staff.Name)
            .Select(staff => new StaffShape(staff.Id, staff.Name, staff.Email, staff.Role, staff.HomeBranchCode, staff.IsActive));
}

public sealed record DashboardBranchShape(BranchCode Code, bool IsActive);
public sealed record LowStockShape(BranchCode BranchCode, Sku Sku, string ProductName, int QuantityOnHand);
public sealed record ShippingVolumeShape(string ZoneId, ServiceLevel ServiceLevel, int Shipments);
public sealed record LineTypeCountShape(BranchCode BranchCode, LineItemType LineType, int Lines);
public sealed record TransactionShape(Guid Id, DateTime OccurredAtUtc, string CounterCode, string ClerkStaffId, int LineCount, decimal GrandTotal, string? PaymentMethod, TransactionStatus Status, string? VoidReason);
public sealed record CatalogueShape(Sku Sku, string Name, string Category, decimal UnitPrice, int QuantityOnHand);
public sealed record ProductShape(Sku Sku, string Name, string Category, decimal UnitPrice, TaxCategory TaxCategory);
public sealed record BranchShape(BranchCode Code, string Name, string Region, TimeSpan OpenTime, TimeSpan CloseTime, string TimeZoneId, bool IsActive, List<string> Terminals);
public sealed record StaffShape(string Id, string Name, string Email, StaffRole Role, BranchCode HomeBranchCode, bool IsActive);

public sealed class ReportQueries(ApexPostDbContext db) : IReportQueries
{
    public async Task<IReadOnlyList<DailySalesRowDto>> GetDailyBranchSalesAsync(string? branchCode, DateOnly date, CancellationToken ct = default)
    {
        // Explicit parameter types: a NULL or a date should not rely on provider type inference.
        var branch = BranchParameter(branchCode);
        var day = new SqlParameter("@Date", SqlDbType.Date) { Value = date.ToDateTime(TimeOnly.MinValue) };
        var rows = await db.Database
            .SqlQuery<DailySalesProcedureRow>($"EXEC dbo.usp_DailyBranchSales @BranchCode = {branch}, @Date = {day}")
            .ToListAsync(ct);
        return rows.Select(row => new DailySalesRowDto(row.BranchCode, row.RetailTotal, row.ServicesTotal)).ToList();
    }

    public async Task<DateOnly?> GetLatestSalesDateAsync(string? branchCode, CancellationToken ct = default)
    {
        var dates = await QueryShapes.LatestSalesDate(db, branchCode).Take(1).ToListAsync(ct);
        return dates.Count == 0 ? null : dates[0];
    }

    public async Task<IReadOnlyList<LowStockRowDto>> GetLowStockAsync(string? branchCode, int threshold, CancellationToken ct = default)
    {
        var branch = BranchParameter(branchCode);
        var rows = await db.Database
            .SqlQuery<LowStockProcedureRow>($"EXEC dbo.usp_LowStockReport @BranchCode = {branch}, @Threshold = {threshold}")
            .ToListAsync(ct);
        return rows.Select(row => new LowStockRowDto(row.BranchCode, row.Sku, row.ProductName, row.QuantityOnHand)).ToList();
    }

    public async Task<IReadOnlyList<ShippingVolumeRowDto>> GetShippingVolumeAsync(string? branchCode, CancellationToken ct = default) =>
        (await QueryShapes.ShippingVolume(db, branchCode).ToListAsync(ct))
            .Select(row => new ShippingVolumeRowDto(row.ZoneId, row.ServiceLevel, row.Shipments))
            .ToList();

    public async Task<IReadOnlyList<MoneyOrderAuditRowDto>> GetMoneyOrderAuditAsync(string? branchCode, CancellationToken ct = default) =>
        (await QueryShapes.MoneyOrderAudit(db, branchCode).ToListAsync(ct))
            .GroupBy(row => row.BranchCode.Value)
            .OrderBy(group => group.Key)
            .Select(group => new MoneyOrderAuditRowDto(
                group.Key,
                group.Where(row => row.LineType == LineItemType.MoneyOrder).Sum(row => row.Lines),
                group.Where(row => row.LineType == LineItemType.IdentityVerification).Sum(row => row.Lines)))
            .ToList();

    private static SqlParameter BranchParameter(string? branchCode) =>
        new("@BranchCode", SqlDbType.VarChar, 10) { Value = (object?)branchCode ?? DBNull.Value };
}

public sealed class DailySalesProcedureRow
{
    public string BranchCode { get; set; } = string.Empty;
    public decimal RetailTotal { get; set; }
    public decimal ServicesTotal { get; set; }
}

public sealed class LowStockProcedureRow
{
    public string BranchCode { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
}

public sealed class DashboardQueries(ApexPostDbContext db) : IDashboardQueries
{
    // These run one after another because a DbContext does not allow overlapping operations.
    public async Task<DashboardCountsDto> GetCountsAsync(string? branchCode, int lowStockThreshold, CancellationToken ct = default)
    {
        var branches = await QueryShapes.DashboardBranches(db, branchCode).ToListAsync(ct);
        var productCount = await db.Products.CountAsync(ct);
        var transactionCount = await QueryShapes.BranchTransactionCount(db, branchCode).CountAsync(ct);
        var lowStock = await QueryShapes.LowStock(db, branchCode, lowStockThreshold).ToListAsync(ct);
        return new DashboardCountsDto(
            branches.Select(branch => branch.Code.Value).ToList(),
            productCount,
            transactionCount,
            branches.Where(branch => !branch.IsActive).Select(branch => branch.Code.Value).ToList(),
            lowStock.Select(row => new LowStockRowDto(row.BranchCode.Value, row.Sku.Value, row.ProductName, row.QuantityOnHand)).ToList());
    }
}

public sealed class ScreenQueries(ApexPostDbContext db) : IScreenQueries
{
    public async Task<IReadOnlyList<CatalogueRowDto>> GetBranchCatalogueAsync(string branchCode, CancellationToken ct = default) =>
        (await QueryShapes.BranchCatalogue(db, branchCode).ToListAsync(ct))
            .Select(row => new CatalogueRowDto(row.Sku.Value, row.Name, row.Category, row.UnitPrice, row.QuantityOnHand))
            .ToList();

    public async Task<IReadOnlyList<ProductRowDto>> GetProductsAsync(CancellationToken ct = default) =>
        (await QueryShapes.Products(db, null).ToListAsync(ct)).Select(ToProductRow).ToList();

    public async Task<ProductRowDto?> GetProductAsync(string sku, CancellationToken ct = default) =>
        (await QueryShapes.Products(db, sku).ToListAsync(ct)).Select(ToProductRow).FirstOrDefault();

    public async Task<IReadOnlyList<BranchRowDto>> GetBranchesAsync(CancellationToken ct = default) =>
        (await QueryShapes.Branches(db, null).AsSplitQuery().ToListAsync(ct)).Select(ToBranchRow).ToList();

    public async Task<BranchRowDto?> GetBranchAsync(string code, CancellationToken ct = default) =>
        (await QueryShapes.Branches(db, code).AsSplitQuery().ToListAsync(ct)).Select(ToBranchRow).FirstOrDefault();

    public async Task<IReadOnlyList<StaffRowDto>> GetStaffAsync(CancellationToken ct = default) =>
        (await QueryShapes.Staff(db, null).ToListAsync(ct)).Select(ToStaffRow).ToList();

    public async Task<StaffRowDto?> GetStaffMemberAsync(string id, CancellationToken ct = default) =>
        (await QueryShapes.Staff(db, id).ToListAsync(ct)).Select(ToStaffRow).FirstOrDefault();

    public async Task<IReadOnlyList<TransactionRowDto>> GetBranchTransactionsAsync(string branchCode, int take, CancellationToken ct = default) =>
        (await QueryShapes.BranchTransactions(db, branchCode, take).ToListAsync(ct))
            .Select(row => new TransactionRowDto(
                row.Id,
                new DateTimeOffset(DateTime.SpecifyKind(row.OccurredAtUtc, DateTimeKind.Utc)),
                row.CounterCode,
                row.ClerkStaffId,
                row.LineCount,
                row.GrandTotal,
                row.PaymentMethod ?? "Pending",
                row.Status,
                row.VoidReason))
            .ToList();

    public async Task<CheckoutOptionsDto?> GetCheckoutOptionsAsync(string branchCode, CancellationToken ct = default)
    {
        var branch = await GetBranchAsync(branchCode, ct);
        if (branch is null)
        {
            return null;
        }

        var stock = (await QueryShapes.BranchCatalogue(db, branchCode).ToListAsync(ct))
            .Select(row => new CheckoutStockRowDto(row.Sku.Value, row.Name, row.UnitPrice, row.QuantityOnHand))
            .ToList();
        return new CheckoutOptionsDto(branch.Code, branch.CounterTerminals, stock);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.ProductCategories.AsNoTracking().OrderBy(category => category.Name).Select(category => category.Name).ToListAsync(ct);

    private static ProductRowDto ToProductRow(ProductShape row) =>
        new(row.Sku.Value, row.Name, row.Category, row.UnitPrice, row.TaxCategory);

    private static BranchRowDto ToBranchRow(BranchShape row) =>
        new(row.Code.Value, row.Name, row.Region, row.OpenTime, row.CloseTime, row.TimeZoneId, row.IsActive, row.Terminals);

    private static StaffRowDto ToStaffRow(StaffShape row) =>
        new(row.Id, row.Name, row.Email, row.Role, row.HomeBranchCode.Value, row.IsActive);
}
