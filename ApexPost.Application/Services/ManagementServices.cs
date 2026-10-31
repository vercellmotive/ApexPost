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
    private readonly IBranchRepository _branches;
    private readonly IStockRepository _stocks;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public BranchManagementService(
        IBranchRepository branches,
        IStockRepository stocks,
        IProductRepository products,
        IUnitOfWork unitOfWork)
    {
        _branches = branches;
        _stocks = stocks;
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<Branch> CreateAsync(BranchRecordInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();
        if (!BranchCodeFormat().IsMatch(code))
        {
            throw new DomainValidationException("Branch codes must look like B006.");
        }

        if (await _branches.GetByCodeAsync(new BranchCode(code), ct) is not null)
        {
            throw new DomainValidationException($"Branch {code} already exists.");
        }

        var branch = Build(code, input, isActive: true);
        _branches.Add(branch);

        // A new branch starts with an empty stock row per product, so stock adjustments work immediately.
        foreach (var product in await _products.GetAllAsync(ct))
        {
            _stocks.Add(new BranchStock { Id = Guid.NewGuid(), BranchCode = branch.Id, ProductSku = product.Id });
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return branch;
    }

    public async Task<Branch> UpdateAsync(string code, BranchRecordInput input, CancellationToken ct = default)
    {
        var existing = await _branches.GetByCodeAsync(new BranchCode(code), ct)
            ?? throw new KeyNotFoundException($"Branch {code} was not found.");
        var updated = Build(existing.Id.Value, input, existing.IsActive);
        _branches.Update(updated);
        await _unitOfWork.SaveChangesAsync(ct);
        return updated;
    }

    public async Task SetActiveAsync(string code, bool isActive, CancellationToken ct = default)
    {
        var existing = await _branches.GetByCodeAsync(new BranchCode(code), ct)
            ?? throw new KeyNotFoundException($"Branch {code} was not found.");
        if (isActive)
        {
            existing.Reopen();
        }
        else
        {
            existing.Close();
        }

        _branches.Update(existing);
        await _unitOfWork.SaveChangesAsync(ct);
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
    private readonly IProductRepository _products;
    private readonly IStockRepository _stocks;
    private readonly IBranchRepository _branches;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPriceRevisionCommand _priceRevision;
    private readonly ICatalogueCache _catalogueCache;

    public ProductManagementService(
        IProductRepository products,
        IStockRepository stocks,
        IBranchRepository branches,
        IUnitOfWork unitOfWork,
        IPriceRevisionCommand priceRevision,
        ICatalogueCache catalogueCache)
    {
        _products = products;
        _stocks = stocks;
        _branches = branches;
        _unitOfWork = unitOfWork;
        _priceRevision = priceRevision;
        _catalogueCache = catalogueCache;
    }

    public async Task<Product> CreateAsync(ProductRecordInput input, CancellationToken ct = default)
    {
        var sku = input.Sku.Trim().ToUpperInvariant();
        if (!SkuFormat().IsMatch(sku))
        {
            throw new DomainValidationException("SKUs must look like SKU-031.");
        }

        if (await _products.GetBySkuAsync(new Sku(sku), ct) is not null)
        {
            throw new DomainValidationException($"Product {sku} already exists.");
        }

        var product = Build(sku, input);
        _products.Add(product);

        // Every branch gets a zero-quantity row, so the product can be stocked with a normal adjustment.
        foreach (var branch in await _branches.GetAllAsync(ct))
        {
            _stocks.Add(new BranchStock { Id = Guid.NewGuid(), BranchCode = branch.Id, ProductSku = product.Id });
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _catalogueCache.Invalidate();
        return product;
    }

    public async Task<Product> UpdateAsync(string sku, ProductRecordInput input, CancellationToken ct = default)
    {
        var existing = await _products.GetBySkuAsync(new Sku(sku), ct)
            ?? throw new KeyNotFoundException($"Product {sku} was not found.");
        var updated = Build(existing.Id.Value, input);
        _products.Update(updated);
        await _unitOfWork.SaveChangesAsync(ct);
        _catalogueCache.Invalidate();
        return updated;
    }

    public async Task<int> RevisePricesAsync(string category, decimal percentChange, CancellationToken ct = default)
    {
        if (!ProductCategories.IsSupported(category))
        {
            throw new DomainValidationException("Select a valid product category.");
        }

        if (percentChange is 0 or <= -100 or > 100)
        {
            throw new DomainValidationException("The price change must be non-zero, above -100% and at most 100%.");
        }

        var revised = await _priceRevision.ReviseByCategoryAsync(category, percentChange, ct);
        _catalogueCache.Invalidate();
        return revised;
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
