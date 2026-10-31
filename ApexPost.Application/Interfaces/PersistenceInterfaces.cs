using ApexPost.Domain.Entities;
using ApexPost.Domain.Common;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Application.Interfaces;

public interface IRepository<TEntity, in TId>
    where TEntity : Entity<TId>
    where TId : notnull
{
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<TEntity?> UpdateAsync(TId id, Action<TEntity> update, CancellationToken ct = default);
    Task SaveAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(TId id, CancellationToken ct = default);
}

public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T candidate);
}

public static class RepositorySpecificationExtensions
{
    public static async Task<IReadOnlyList<TEntity>> FindAsync<TEntity, TId>(
        this IRepository<TEntity, TId> repository,
        ISpecification<TEntity> specification,
        CancellationToken ct = default)
        where TEntity : Entity<TId>
        where TId : notnull =>
        (await repository.GetAllAsync(ct)).Where(specification.IsSatisfiedBy).ToList();
}

public sealed record PaidTransactionsSpecification(string? BranchCode) : ISpecification<Transaction>
{
    public bool IsSatisfiedBy(Transaction candidate) =>
        candidate.Status == TransactionStatus.Paid
        && (BranchCode is null || candidate.BranchCode.Value == BranchCode);
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

public interface IShippingZoneLookup
{
    ValueTask<ShippingZone?> FindByNameAsync(string name, CancellationToken ct = default);
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
