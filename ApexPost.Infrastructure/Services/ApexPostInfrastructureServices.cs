using System.Collections.Concurrent;
using System.Security.Cryptography;
using ApexPost.Application.Interfaces;
using ApexPost.Application.Pricing;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Events;
using ApexPost.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;

namespace ApexPost.Infrastructure.Services;

public sealed class CachedShippingZoneLookup : IShippingZoneLookup
{
    private const string CacheKey = "apexpost-shipping-zones";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly IShippingZoneRepository _zones;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public CachedShippingZoneLookup(IShippingZoneRepository zones, IMemoryCache cache)
    {
        _zones = zones;
        _cache = cache;
    }

    public ValueTask<ShippingZone?> FindByNameAsync(string name, CancellationToken ct = default) =>
        _cache.TryGetValue(CacheKey, out Dictionary<string, ShippingZone>? zones)
            ? ValueTask.FromResult(zones!.GetValueOrDefault(name))
            : LoadAndFindAsync(name, ct);

    private async ValueTask<ShippingZone?> LoadAndFindAsync(string name, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            if (!_cache.TryGetValue(CacheKey, out Dictionary<string, ShippingZone>? zones))
            {
                zones = (await _zones.GetAllAsync(ct)).ToDictionary(
                    zone => zone.Name,
                    StringComparer.OrdinalIgnoreCase);
                _cache.Set(CacheKey, zones, _ttl);
            }

            return zones!.GetValueOrDefault(name);
        }
        finally
        {
            Gate.Release();
        }
    }
}

// The limit must span requests, so the gate is a singleton while the service is scoped with its repositories.
public sealed class CourierRateGate
{
    public CourierRateGate(int maxConcurrentRequests)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrentRequests);
        Slots = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
    }

    public SemaphoreSlim Slots { get; }
}

public class SimulatedCourierRateService : ICourierRateService
{
    private readonly SemaphoreSlim _gate;
    private readonly IPricingStrategy _pricing;
    private readonly IShippingZoneLookup _zones;
    private readonly TimeSpan _simulatedDelay;

    public SimulatedCourierRateService(
        IPricingStrategy pricing,
        IShippingZoneLookup zones,
        CourierRateGate gate,
        TimeSpan simulatedDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(simulatedDelay, TimeSpan.Zero);
        _pricing = pricing;
        _zones = zones;
        _gate = gate.Slots;
        _simulatedDelay = simulatedDelay;
    }

    public async Task<Money> GetEstimatedCourierRateAsync(ShippingDetails details, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        ShippingZone zone;
        try
        {
            // The gate protects the slow partner call; pricing below is pure CPU and needs no slot.
            await Task.Delay(_simulatedDelay, ct);
            zone = await _zones.FindByNameAsync(details.DestinationZone, ct)
                ?? throw new DomainValidationException($"Shipping destination {details.DestinationZone} is not configured.");
        }
        finally
        {
            _gate.Release();
        }

        return _pricing.CalculateShippingPrice(details, zone.Multiplier);
    }
}

public sealed class SimulatedIdentityVerificationService : IIdentityVerificationService
{
    public Task<IdVerificationDetails> VerifyAsync(
        DocumentType documentType,
        string documentNumber,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        if (!Enum.IsDefined(documentType))
        {
            throw new DomainValidationException("A supported identity document type is required.");
        }

        var normalizedNumber = documentNumber.Trim();
        if (normalizedNumber.Length is < 4 or > 64
            || normalizedNumber.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new DomainValidationException("The document number must contain 4 to 64 letters or digits.");
        }

        var formatIsValid = normalizedNumber.Length is >= 8 and <= 20;
        return Task.FromResult(IdVerificationDetails.FromDocumentNumber(
            documentType,
            normalizedNumber,
            formatIsValid));
    }
}

public sealed class SimulatedCourierTrackingService : ICourierTrackingService
{
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _simulatedDelay;

    public SimulatedCourierTrackingService(TimeSpan timeout, TimeSpan simulatedDelay)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Tracking timeout must be positive.");
        }

        if (simulatedDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(simulatedDelay), "Simulated lookup delay cannot be negative.");
        }

        _timeout = timeout;
        _simulatedDelay = simulatedDelay;
    }

    public async Task<CourierTrackingStatus> GetStatusAsync(string trackingId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingId);
        if (trackingId.Length > 64
            || trackingId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            throw new DomainValidationException("The tracking identifier is invalid.");
        }

        using var timeout = new CancellationTokenSource(_timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            await Task.Delay(_simulatedDelay, linkedCancellation.Token);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new CourierTrackingTimeoutException("The courier tracking lookup exceeded its timeout.", exception);
        }

        return new CourierTrackingStatus(
            trackingId,
            "In transit",
            DateTimeOffset.UtcNow.AddDays(2),
            "Zone 3");
    }
}

public class SimulatedNotificationService : INotificationService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _alerts = new(StringComparer.OrdinalIgnoreCase);

    public Task NotifyStockLowAsync(BranchCode branchCode, Sku sku, int remaining, CancellationToken ct = default)
    {
        var message = $"Low stock alert for {sku.Value}: {remaining} remaining at branch {branchCode.Value}.";
        AddAlert(branchCode.Value, message);
        return Task.CompletedTask;
    }

    public Task NotifyHighValueMoneyOrderAsync(BranchCode branchCode, Guid transactionId, Money amount, CancellationToken ct = default)
    {
        var message = $"High-value money order {transactionId}: {amount.Amount:C}.";
        AddAlert(branchCode.Value, message);
        return Task.CompletedTask;
    }

    public Task NotifyTransactionVoidedAsync(BranchCode branchCode, Guid transactionId, string reason, CancellationToken ct = default)
    {
        var message = $"Transaction {transactionId} was voided or refunded: {reason}";
        AddAlert(branchCode.Value, message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetRecentAlertsAsync(BranchCode branchCode, CancellationToken ct = default)
    {
        IReadOnlyList<string> items = _alerts.TryGetValue(branchCode.Value, out var alerts)
            ? alerts.ToArray()
            : Array.Empty<string>();
        return Task.FromResult(items);
    }

    private void AddAlert(string key, string value)
    {
        var alerts = _alerts.GetOrAdd(key, _ => new ConcurrentQueue<string>());
        alerts.Enqueue(value);
        while (alerts.Count > 25)
        {
            alerts.TryDequeue(out _);
        }
    }
}

public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly INotificationService _notifications;
    private readonly TransactionRules _rules;

    public DomainEventDispatcher(INotificationService notifications, TransactionRules rules)
    {
        _notifications = notifications;
        _rules = rules;
    }

    public Task DispatchAsync(ApexPost.Domain.Common.IDomainEvent domainEvent, CancellationToken ct = default) =>
        domainEvent switch
        {
            StockLowEvent stockLow => _notifications.NotifyStockLowAsync(stockLow.BranchCode, stockLow.Sku, stockLow.RemainingQuantity, ct),
            MoneyOrderIssuedEvent moneyOrder when moneyOrder.Amount > _rules.HighValueMoneyOrderThreshold =>
                _notifications.NotifyHighValueMoneyOrderAsync(moneyOrder.BranchCode, moneyOrder.TransactionId, moneyOrder.Amount, ct),
            TransactionVoidedEvent voided =>
                _notifications.NotifyTransactionVoidedAsync(voided.BranchCode, voided.TransactionId, voided.Reason, ct),
            _ => Task.CompletedTask
        };
}

// Scoped with its repository; the memory cache and fill gate are shared across requests.
public sealed class ProductCatalogueService : ICatalogueService, ICatalogueCache
{
    private const string CacheKey = "apexpost-catalogue";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly IProductRepository _products;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public ProductCatalogueService(IProductRepository products, IMemoryCache cache)
    {
        _products = products;
        _cache = cache;
    }

    public void Invalidate() => _cache.Remove(CacheKey);

    public ValueTask<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<Product>? cached))
        {
            return ValueTask.FromResult(cached ?? Array.Empty<Product>());
        }

        return GetAndCacheAsync(ct);
    }

    public async ValueTask<Product?> GetProductBySkuAsync(Sku sku, CancellationToken ct = default)
    {
        var products = await GetProductsAsync(ct);
        return products.FirstOrDefault(product => product.Id == sku);
    }

    private async ValueTask<IReadOnlyList<Product>> GetAndCacheAsync(CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<Product>? cached))
            {
                return cached ?? Array.Empty<Product>();
            }

            var products = await _products.GetAllAsync(ct);
            _cache.Set(CacheKey, products, _ttl);
            return products;
        }
        finally
        {
            Gate.Release();
        }
    }
}

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private static readonly byte[] UnknownAccountSalt = new byte[SaltSize];

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string? passwordHash)
    {
        var parts = passwordHash?.Split('$');
        if (parts is not ["PBKDF2-SHA256", var iterationText, var saltText, var keyText]
            || !int.TryParse(iterationText, out var iterations)
            || iterations <= 0)
        {
            // Spend the same effort as a real check so unknown accounts are not distinguishable by timing.
            Rfc2898DeriveBytes.Pbkdf2(password, UnknownAccountSalt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return false;
        }

        var expected = Convert.FromBase64String(keyText);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String(saltText),
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
