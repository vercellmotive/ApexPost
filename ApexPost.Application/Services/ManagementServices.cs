using System.Text.RegularExpressions;
using ApexPost.Application.Interfaces;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed record BranchRecordInput(
    string Code,
    string Name,
    string Region,
    TimeSpan OpenTime,
    TimeSpan CloseTime,
    string TimeZoneId,
    IReadOnlyList<string> CounterTerminals);

public sealed record ProductRecordInput(
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    TaxCategory TaxCategory);

public sealed partial class BranchManagementService
{
    private readonly IRepository<Branch, BranchCode> _branches;
    private readonly IRepository<BranchStock, Guid> _stocks;
    private readonly IRepository<Product, Sku> _products;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public BranchManagementService(
        IRepository<Branch, BranchCode> branches,
        IRepository<BranchStock, Guid> stocks,
        IRepository<Product, Sku> products)
    {
        _branches = branches;
        _stocks = stocks;
        _products = products;
    }

    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken ct = default) =>
        (await _branches.GetAllAsync(ct)).OrderBy(branch => branch.Id.Value).ToList();

    public Task<Branch?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        _branches.GetByIdAsync(new BranchCode(code), ct);

    public async Task<Branch> CreateAsync(BranchRecordInput input, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            var code = input.Code.Trim().ToUpperInvariant();
            if (!BranchCodeFormat().IsMatch(code))
            {
                throw new DomainValidationException("Branch codes must look like B006.");
            }

            if (await _branches.GetByIdAsync(new BranchCode(code), ct) is not null)
            {
                throw new DomainValidationException($"Branch {code} already exists.");
            }

            var branch = Build(code, input, isActive: true);
            await _branches.SaveAsync(branch, ct);

            // A new branch starts with an empty stock row per product, so stock adjustments work immediately.
            foreach (var product in await _products.GetAllAsync(ct))
            {
                await _stocks.SaveAsync(
                    new BranchStock { Id = Guid.NewGuid(), BranchCode = branch.Id, ProductSku = product.Id },
                    ct);
            }

            return branch;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Branch> UpdateAsync(string code, BranchRecordInput input, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            var existing = await _branches.GetByIdAsync(new BranchCode(code), ct)
                ?? throw new KeyNotFoundException($"Branch {code} was not found.");
            var updated = Build(existing.Id.Value, input, existing.IsActive);
            await _branches.SaveAsync(updated, ct);
            return updated;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task SetActiveAsync(string code, bool isActive, CancellationToken ct = default)
    {
        var updated = await _branches.UpdateAsync(new BranchCode(code), branch => branch.IsActive = isActive, ct);
        if (updated is null)
        {
            throw new KeyNotFoundException($"Branch {code} was not found.");
        }
    }

    private static Branch Build(string code, BranchRecordInput input, bool isActive)
    {
        var terminals = input.CounterTerminals
            .Select(terminal => terminal.Trim())
            .Where(terminal => terminal.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Region))
        {
            throw new DomainValidationException("Branch name and region are required.");
        }

        if (terminals.Count == 0)
        {
            throw new DomainValidationException("A branch needs at least one counter terminal.");
        }

        if (input.OpenTime >= input.CloseTime)
        {
            throw new DomainValidationException("The opening time must be before the closing time.");
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new DomainValidationException($"Time zone '{input.TimeZoneId}' is not recognised.");
        }

        return new Branch
        {
            Id = new BranchCode(code),
            Name = input.Name.Trim(),
            Region = input.Region.Trim(),
            OpenTime = input.OpenTime,
            CloseTime = input.CloseTime,
            TimeZoneId = input.TimeZoneId,
            IsActive = isActive,
            CounterTerminals = terminals
        };
    }

    [GeneratedRegex("^[A-Z][0-9]{3}$")]
    private static partial Regex BranchCodeFormat();
}

public sealed partial class ProductManagementService
{
    private readonly IRepository<Product, Sku> _products;
    private readonly IRepository<BranchStock, Guid> _stocks;
    private readonly IRepository<Branch, BranchCode> _branches;
    private readonly ICatalogueCache _catalogueCache;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public ProductManagementService(
        IRepository<Product, Sku> products,
        IRepository<BranchStock, Guid> stocks,
        IRepository<Branch, BranchCode> branches,
        ICatalogueCache catalogueCache)
    {
        _products = products;
        _stocks = stocks;
        _branches = branches;
        _catalogueCache = catalogueCache;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default) =>
        (await _products.GetAllAsync(ct)).OrderBy(product => product.Id.Value).ToList();

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default) =>
        _products.GetByIdAsync(new Sku(sku), ct);

    public async Task<Product> CreateAsync(ProductRecordInput input, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            var sku = input.Sku.Trim().ToUpperInvariant();
            if (!SkuFormat().IsMatch(sku))
            {
                throw new DomainValidationException("SKUs must look like SKU-031.");
            }

            if (await _products.GetByIdAsync(new Sku(sku), ct) is not null)
            {
                throw new DomainValidationException($"Product {sku} already exists.");
            }

            var product = Build(sku, input);
            await _products.SaveAsync(product, ct);

            // Every branch gets a zero-quantity row, so the product can be stocked with a normal adjustment.
            foreach (var branch in await _branches.GetAllAsync(ct))
            {
                await _stocks.SaveAsync(
                    new BranchStock { Id = Guid.NewGuid(), BranchCode = branch.Id, ProductSku = product.Id },
                    ct);
            }

            _catalogueCache.Invalidate();
            return product;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Product> UpdateAsync(string sku, ProductRecordInput input, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            var existing = await _products.GetByIdAsync(new Sku(sku), ct)
                ?? throw new KeyNotFoundException($"Product {sku} was not found.");
            var updated = Build(existing.Id.Value, input);
            await _products.SaveAsync(updated, ct);
            _catalogueCache.Invalidate();
            return updated;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static Product Build(string sku, ProductRecordInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new DomainValidationException("A product name is required.");
        }

        if (!ProductCategories.IsSupported(input.Category))
        {
            throw new DomainValidationException("Select a valid product category.");
        }

        if (input.UnitPrice <= 0)
        {
            throw new DomainValidationException("The unit price must be greater than zero.");
        }

        if (!Enum.IsDefined(input.TaxCategory))
        {
            throw new DomainValidationException("Select a valid tax category.");
        }

        return new Product
        {
            Id = new Sku(sku),
            Name = input.Name.Trim(),
            Category = ProductCategories.All.First(category =>
                category.Equals(input.Category, StringComparison.OrdinalIgnoreCase)),
            UnitPrice = new Money(input.UnitPrice),
            TaxCategory = input.TaxCategory
        };
    }

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{2,19}$")]
    private static partial Regex SkuFormat();
}
