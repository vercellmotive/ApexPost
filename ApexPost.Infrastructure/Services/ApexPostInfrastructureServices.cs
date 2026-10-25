using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ApexPost.Application.Interfaces;
using ApexPost.Application.Pricing;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Events;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ApexPost.Infrastructure.Services;

public class JsonFileRepository<T> : IRepository<T> where T : class
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public JsonFileRepository(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        _filePath = Path.Combine(dataDirectory, $"{typeof(T).Name.ToLowerInvariant()}.json");
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            return await ReadAllUnlockedAsync(ct);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        var items = await GetAllAsync(ct);
        var idProperty = typeof(T).GetProperty("Id");
        return idProperty is null
            ? null
            : items.FirstOrDefault(item => Equals(idProperty.GetValue(item), id));
    }

    public async Task<T?> UpdateAsync(object id, Action<T> update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        await _fileLock.WaitAsync(ct);
        try
        {
            var items = (await ReadAllUnlockedAsync(ct)).ToList();
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty is null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} does not expose an Id property.");
            }

            var entity = items.FirstOrDefault(item => Equals(idProperty.GetValue(item), id));
            if (entity is null)
            {
                return null;
            }

            update(entity);
            await WriteAllUnlockedAsync(items, ct);
            return entity;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(T entity, CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            var items = (await ReadAllUnlockedAsync(ct)).ToList();
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty is null)
            {
                items.Add(entity);
            }
            else
            {
                var idValue = idProperty.GetValue(entity);
                var index = items.FindIndex(item => Equals(idProperty.GetValue(item), idValue));
                if (index >= 0)
                {
                    items[index] = entity;
                }
                else
                {
                    items.Add(entity);
                }
            }

            await WriteAllUnlockedAsync(items, ct);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteAsync(object id, CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            var items = (await ReadAllUnlockedAsync(ct)).ToList();
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty is null)
            {
                return;
            }

            var filtered = items.Where(item => !Equals(idProperty.GetValue(item), id)).ToList();
            await WriteAllUnlockedAsync(filtered, ct);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<T>> ReadAllUnlockedAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<T>>(stream, _options, ct) ?? [];
    }

    private async Task WriteAllUnlockedAsync(List<T> items, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, items, _options, ct);
                await stream.FlushAsync(ct);
            }

            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

public class JsonAuditStore : IAuditStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonAuditStore(IWebHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "Data", "audit-log.jsonl");
    }

    public async Task AppendAsync(AuditEntry entry, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await JsonSerializer.SerializeAsync(stream, entry, cancellationToken: ct);
            await stream.WriteAsync("\n"u8.ToArray(), ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await ReadAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AuditEntry>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<AuditEntry>();
        }

        var entries = new List<AuditEntry>();
        using var reader = new StreamReader(new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true));
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                entries.Add(JsonSerializer.Deserialize<AuditEntry>(line)
                    ?? throw new JsonException("Audit log contains an invalid entry."));
            }
        }

        return entries;
    }
}

public class SimulatedCourierRateService : ICourierRateService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IPricingStrategy _pricing;
    private readonly IRepository<ShippingZone> _shippingZones;

    public SimulatedCourierRateService(IPricingStrategy pricing, IRepository<ShippingZone> shippingZones)
    {
        _pricing = pricing;
        _shippingZones = shippingZones;
    }

    public async Task<Money> GetEstimatedCourierRateAsync(ShippingDetails details, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var rates = await _shippingZones.GetAllAsync(ct);
            var zone = rates.FirstOrDefault(rate => rate.Name.Equals(details.DestinationZone, StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainValidationException($"Shipping destination {details.DestinationZone} is not configured.");
            return _pricing.CalculateShippingPrice(details, zone.Multiplier);
        }
        finally
        {
            _gate.Release();
        }
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

    public Task NotifyHighValueMoneyOrderAsync(Guid transactionId, Money amount, CancellationToken ct = default)
    {
        var message = $"High-value money order {transactionId}: {amount.Amount:C}.";
        AddAlert("finance", message);
        return Task.CompletedTask;
    }

    public Task NotifyTransactionVoidedAsync(Guid transactionId, string reason, CancellationToken ct = default)
    {
        var message = $"Transaction {transactionId} was voided or refunded: {reason}";
        AddAlert("ops", message);
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
                _notifications.NotifyHighValueMoneyOrderAsync(moneyOrder.TransactionId, moneyOrder.Amount, ct),
            TransactionVoidedEvent voided =>
                _notifications.NotifyTransactionVoidedAsync(voided.TransactionId, voided.Reason, ct),
            _ => Task.CompletedTask
        };
}

public sealed class ProductCatalogueService : ICatalogueService
{
    private readonly IRepository<Product> _products;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ProductCatalogueService(IRepository<Product> products, IMemoryCache cache)
    {
        _products = products;
        _cache = cache;
    }

    public ValueTask<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue("apexpost-catalogue", out IReadOnlyList<Product>? cached))
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
        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue("apexpost-catalogue", out IReadOnlyList<Product>? cached))
            {
                return cached ?? Array.Empty<Product>();
            }

            var products = await _products.GetAllAsync(ct);
            _cache.Set("apexpost-catalogue", products, _ttl);
            return products;
        }
        finally
        {
            _gate.Release();
        }
    }
}
