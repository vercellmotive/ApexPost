using ApexPost.Domain.Entities;
using ApexPost.Domain.Common;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Application.Interfaces;

public interface IRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<T?> UpdateAsync(object id, Action<T> update, CancellationToken ct = default);
    Task SaveAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(object id, CancellationToken ct = default);
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
    Task NotifyHighValueMoneyOrderAsync(Guid transactionId, Money amount, CancellationToken ct = default);
    Task NotifyTransactionVoidedAsync(Guid transactionId, string reason, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRecentAlertsAsync(BranchCode branchCode, CancellationToken ct = default);
}
