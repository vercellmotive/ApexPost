using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Interfaces;

// Repositories stage changes only: they never commit and never expose IQueryable.
public interface IBranchRepository
{
    Task<Branch?> GetByCodeAsync(BranchCode code, CancellationToken ct = default);
    Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken ct = default);
    void Add(Branch branch);
    void Update(Branch branch);
}

public interface IStaffRepository
{
    Task<Staff?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Staff?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, string? exceptStaffId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllIdsAsync(CancellationToken ct = default);
    void Add(Staff staff);
    void Update(Staff staff);
}

public interface IProductRepository
{
    Task<Product?> GetBySkuAsync(Sku sku, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);
    void Add(Product product);
    void Update(Product product);
}

public interface IStockRepository
{
    Task<BranchStock?> GetAsync(BranchCode branchCode, Sku sku, CancellationToken ct = default);
    void Add(BranchStock stock);
}

public interface ICounterTransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Transaction transaction);
    void Update(Transaction transaction);
}

public interface IShippingZoneRepository
{
    Task<IReadOnlyList<ShippingZone>> GetAllAsync(CancellationToken ct = default);
}

// One commit per use case. Failures surface as the Application exceptions below, never as EF types.
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);

    // Forgets everything loaded or staged, so a retried use case reads fresh data.
    void ResetTracking();
}

public sealed class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class DuplicateValueException(string message, Exception? innerException = null)
    : Exception(message, innerException);

// Set-based and procedure-backed writes run immediately, so they are commands rather than repository stages.
public interface IStockAdjustmentCommand
{
    Task<int> AdjustAsync(
        BranchCode branchCode,
        Sku sku,
        int delta,
        StockAdjustmentReason reason,
        string changedBy,
        CancellationToken ct = default);
}

public interface IPriceRevisionCommand
{
    Task<int> ReviseByCategoryAsync(string category, decimal percentChange, CancellationToken ct = default);
}

public interface ICurrentUser
{
    string Name { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string? passwordHash);
}

public interface ICatalogueCache
{
    void Invalidate();
}

public interface IShippingZoneLookup
{
    ValueTask<ShippingZone?> FindByNameAsync(string name, CancellationToken ct = default);
}

public interface ICatalogueService
{
    ValueTask<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct = default);
    ValueTask<Product?> GetProductBySkuAsync(Sku sku, CancellationToken ct = default);
}

public record AuditEntry(
    Guid Id,
    string CorrelationId,
    string StaffId,
    string Role,
    string BranchCode,
    string Controller,
    string Action,
    string Parameters,
    int StatusCode,
    long DurationMs,
    DateTimeOffset TimestampUtc);

public interface IAuditStore
{
    Task AppendAsync(AuditEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntry>> GetAllAsync(CancellationToken ct = default);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

public interface ICourierRateService
{
    Task<Money> GetEstimatedCourierRateAsync(ShippingDetails details, CancellationToken ct = default);
}

public sealed record CourierTrackingStatus(
    string TrackingId,
    string Status,
    DateTimeOffset EstimatedDeliveryUtc,
    string DestinationZone);

public sealed class CourierTrackingTimeoutException : TimeoutException
{
    public CourierTrackingTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public interface ICourierTrackingService
{
    Task<CourierTrackingStatus> GetStatusAsync(string trackingId, CancellationToken ct = default);
}

public interface IIdentityVerificationService
{
    Task<IdVerificationDetails> VerifyAsync(
        DocumentType documentType,
        string documentNumber,
        CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyStockLowAsync(BranchCode branchCode, Sku sku, int remaining, CancellationToken ct = default);
    Task NotifyHighValueMoneyOrderAsync(BranchCode branchCode, Guid transactionId, Money amount, CancellationToken ct = default);
    Task NotifyTransactionVoidedAsync(BranchCode branchCode, Guid transactionId, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRecentAlertsAsync(BranchCode branchCode, CancellationToken ct = default);
}
