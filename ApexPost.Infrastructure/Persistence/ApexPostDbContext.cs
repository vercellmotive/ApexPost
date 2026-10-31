using ApexPost.Application.Interfaces;
using ApexPost.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence;

public sealed class ApexPostDbContext : DbContext
{
    private readonly ICurrentUser? _currentUser;

    public ApexPostDbContext(DbContextOptions<ApexPostDbContext> options, ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<RoleRecord> Roles => Set<RoleRecord>();
    public DbSet<ProductCategoryRecord> ProductCategories => Set<ProductCategoryRecord>();
    public DbSet<BranchRecord> Branches => Set<BranchRecord>();
    public DbSet<CounterRecord> Counters => Set<CounterRecord>();
    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<ProductRecord> Products => Set<ProductRecord>();
    public DbSet<BranchStock> BranchStock => Set<BranchStock>();
    public DbSet<StockAdjustmentRecord> StockAdjustments => Set<StockAdjustmentRecord>();
    public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();
    public DbSet<ShippingRateRecord> ShippingRates => Set<ShippingRateRecord>();
    public DbSet<CounterTransactionRecord> CounterTransactions => Set<CounterTransactionRecord>();
    public DbSet<TransactionLineRecord> TransactionLines => Set<TransactionLineRecord>();
    public DbSet<ShipmentRecord> Shipments => Set<ShipmentRecord>();
    public DbSet<MoneyOrderRecord> MoneyOrders => Set<MoneyOrderRecord>();
    public DbSet<IdentityVerificationRecord> IdentityVerifications => Set<IdentityVerificationRecord>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<AuditEntryRecord> AuditEntries => Set<AuditEntryRecord>();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditColumns();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApexPostDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2");
    }

    private void StampAuditColumns()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Metadata.FindProperty(AuditColumns.CreatedBy) is null)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Property(AuditColumns.CreatedAtUtc).CurrentValue = now;
                entry.Property(AuditColumns.CreatedBy).CurrentValue = _currentUser?.Name ?? "system";
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(AuditColumns.ModifiedAtUtc).CurrentValue = now;
            }
        }
    }
}
