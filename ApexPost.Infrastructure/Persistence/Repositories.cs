using ApexPost.Application.Interfaces;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure.Persistence;

public sealed class BranchRepository(ApexPostDbContext db) : IBranchRepository
{
    public async Task<Branch?> GetByCodeAsync(BranchCode code, CancellationToken ct = default)
    {
        var record = await QueryShapes.TrackedBranch(db, code).FirstOrDefaultAsync(ct);
        return record is null ? null : PersistenceMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken ct = default) =>
        (await db.Branches.AsNoTracking().Include(branch => branch.Counters).OrderBy(branch => branch.Code).ToListAsync(ct))
            .Select(PersistenceMapper.ToDomain)
            .ToList();

    public void Add(Branch branch) => db.Branches.Add(PersistenceMapper.ToRecord(branch));

    public void Update(Branch branch)
    {
        var record = db.Branches.Local.FirstOrDefault(item => item.Code == branch.Id)
            ?? throw new InvalidOperationException($"Branch {branch.Id.Value} must be loaded before it is updated.");
        PersistenceMapper.CopyTo(branch, record);
    }
}

public sealed class StaffRepository(ApexPostDbContext db) : IStaffRepository
{
    public Task<Staff?> GetByIdAsync(string id, CancellationToken ct = default) =>
        db.Staff.FirstOrDefaultAsync(staff => staff.Id == id, ct);

    public Task<Staff?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.Staff.AsNoTracking().FirstOrDefaultAsync(staff => staff.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, string? exceptStaffId, CancellationToken ct = default) =>
        db.Staff.AnyAsync(staff => staff.Email == email && staff.Id != exceptStaffId, ct);

    public async Task<IReadOnlyList<string>> GetAllIdsAsync(CancellationToken ct = default) =>
        await db.Staff.AsNoTracking().Select(staff => staff.Id).ToListAsync(ct);

    public void Add(Staff staff) => db.Staff.Add(staff);

    public void Update(Staff staff)
    {
        var tracked = db.Staff.Local.FirstOrDefault(item => item.Id == staff.Id)
            ?? throw new InvalidOperationException($"Staff member {staff.Id} must be loaded before they are updated.");
        if (!ReferenceEquals(tracked, staff))
        {
            db.Entry(tracked).CurrentValues.SetValues(staff);
        }
    }
}

public sealed class ProductRepository(ApexPostDbContext db) : IProductRepository
{
    public async Task<Product?> GetBySkuAsync(Sku sku, CancellationToken ct = default)
    {
        var record = await QueryShapes.TrackedProduct(db, sku).FirstOrDefaultAsync(ct);
        return record is null ? null : PersistenceMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) =>
        (await db.Products.AsNoTracking().Include(product => product.Category).OrderBy(product => product.Sku).ToListAsync(ct))
            .Select(PersistenceMapper.ToDomain)
            .ToList();

    public void Add(Product product) => db.Products.Add(PersistenceMapper.ToRecord(product));

    public void Update(Product product)
    {
        var record = db.Products.Local.FirstOrDefault(item => item.Sku == product.Id)
            ?? throw new InvalidOperationException($"Product {product.Id.Value} must be loaded before it is updated.");
        PersistenceMapper.CopyTo(product, record);
    }
}

public sealed class StockRepository(ApexPostDbContext db) : IStockRepository
{
    public Task<BranchStock?> GetAsync(BranchCode branchCode, Sku sku, CancellationToken ct = default) =>
        QueryShapes.TrackedStock(db, branchCode, sku).FirstOrDefaultAsync(ct);

    public void Add(BranchStock stock) => db.BranchStock.Add(stock);
}

public sealed class CounterTransactionRepository(ApexPostDbContext db) : ICounterTransactionRepository
{
    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var record = await QueryShapes.TrackedTransaction(db, id).FirstOrDefaultAsync(ct);
        return record is null ? null : PersistenceMapper.ToDomain(record);
    }

    public void Add(Transaction transaction) => db.CounterTransactions.Add(PersistenceMapper.ToRecord(transaction));

    public void Update(Transaction transaction)
    {
        var record = db.CounterTransactions.Local.FirstOrDefault(item => item.Id == transaction.Id)
            ?? throw new InvalidOperationException($"Transaction {transaction.Id} must be loaded before it is updated.");
        PersistenceMapper.CopyStateTo(transaction, record);
    }
}

public sealed class ShippingZoneRepository(ApexPostDbContext db) : IShippingZoneRepository
{
    public async Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken ct = default) =>
        await db.ShippingZones.AsNoTracking().ToListAsync(ct);
}

public sealed class UnitOfWork(ApexPostDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "This record was changed by someone else at the same time. Reload the page and try again.",
                exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new DuplicateValueException("A record with the same unique value already exists.", exception);
        }
    }

    public void DiscardPendingChanges() => db.ChangeTracker.Clear();

    // Retries must repeat the whole unit, so the transaction runs inside the execution strategy.
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            await work(token);
            await transaction.CommitAsync(token);
        }, ct);
    }
}

public sealed class StockAdjustmentCommand(ApexPostDbContext db) : IStockAdjustmentCommand
{
    public async Task<int> AdjustAsync(
        BranchCode branchCode,
        Sku sku,
        int delta,
        StockAdjustmentReason reason,
        string changedBy,
        CancellationToken ct = default)
    {
        var branch = branchCode.Value;
        var product = sku.Value;
        var reasonCode = reason.ToString();
        try
        {
            var rows = await db.Database
                .SqlQuery<StockAdjustmentResult>($"EXEC dbo.usp_AdjustStock @BranchCode = {branch}, @Sku = {product}, @Delta = {delta}, @Reason = {reasonCode}, @ChangedBy = {changedBy}")
                .ToListAsync(ct);
            return rows.Single().QuantityOnHand;
        }
        catch (SqlException exception) when (exception.Number == StoredProcedureErrors.NotFound)
        {
            throw new KeyNotFoundException(exception.Message, exception);
        }
        catch (SqlException exception) when (exception.Number >= StoredProcedureErrors.FirstValidationError)
        {
            throw new DomainValidationException(exception.Message);
        }
    }

}

public sealed class StockAdjustmentResult
{
    public int QuantityOnHand { get; set; }
}

public static class StoredProcedureErrors
{
    public const int FirstValidationError = 50000;
    public const int NotFound = 50404;
}

public sealed class PriceRevisionCommand(ApexPostDbContext db) : IPriceRevisionCommand
{
    public Task<int> ReviseByCategoryAsync(string category, decimal percentChange, CancellationToken ct = default)
    {
        var categoryId = ReferenceData.CategoryIds[category];
        var factor = 1m + (percentChange / 100m);
        var now = DateTime.UtcNow;
        return db.Products
            .Where(product => product.CategoryId == categoryId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(product => product.UnitPrice, product => Math.Round(product.UnitPrice * factor, 2))
                    .SetProperty(product => EF.Property<DateTime?>(product, AuditColumns.ModifiedAtUtc), now),
                ct);
    }
}

// Audit rows commit on their own context and scope, so they survive a failed business transaction
// and an audit failure can never roll one back (see ADR-31).
public sealed class SqlAuditStore(IServiceScopeFactory scopes) : IAuditStore
{
    public async Task AppendAsync(AuditEntry entry, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        db.AuditEntries.Add(new AuditEntryRecord
        {
            Id = entry.Id,
            CorrelationId = entry.CorrelationId,
            StaffId = entry.StaffId,
            Role = entry.Role,
            BranchCode = entry.BranchCode,
            Controller = entry.Controller,
            Action = entry.Action,
            Parameters = entry.Parameters,
            StatusCode = entry.StatusCode,
            DurationMs = entry.DurationMs,
            TimestampUtc = entry.TimestampUtc.UtcDateTime
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        return await db.AuditEntries
            .AsNoTracking()
            .OrderBy(entry => entry.TimestampUtc)
            .Select(entry => new AuditEntry(
                entry.Id,
                entry.CorrelationId,
                entry.StaffId,
                entry.Role,
                entry.BranchCode,
                entry.Controller,
                entry.Action,
                entry.Parameters,
                entry.StatusCode,
                entry.DurationMs,
                new DateTimeOffset(entry.TimestampUtc, TimeSpan.Zero)))
            .ToListAsync(ct);
    }
}
