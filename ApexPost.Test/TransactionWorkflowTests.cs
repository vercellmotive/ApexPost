using ApexPost.Application.Interfaces;
using ApexPost.Application.Services;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Test;

public sealed class TransactionWorkflowTests
{
    [Fact]
    public async Task FailedBusinessValidation_RestoresAllReservedStock()
    {
        var stock = CreateStock(10);
        var transactionRepository = new MemoryRepository<Transaction>();
        var service = CreateService(stock, transactionRepository: transactionRepository);
        var transaction = CreateTransaction();
        service.AddMoneyOrder(transaction, new Money(1500m), "Sender", "Recipient");

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.CompleteWithRetailItemsAsync(
                transaction,
                [new RetailItemRequest(new Sku("SKU-001"), 3)],
                "Cash"));

        Assert.Equal(10, stock.QuantityOnHand);
        Assert.Empty(await transactionRepository.GetAllAsync());
    }

    [Fact]
    public async Task LaterReservationFailure_RestoresEarlierReservations()
    {
        var stock = CreateStock(10);
        var transactionRepository = new MemoryRepository<Transaction>();
        var service = CreateService(stock, transactionRepository: transactionRepository);
        var transaction = CreateTransaction();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CompleteWithRetailItemsAsync(
                transaction,
                [
                    new RetailItemRequest(new Sku("SKU-001"), 3),
                    new RetailItemRequest(new Sku("SKU-404"), 1)
                ],
                "Cash"));

        Assert.Equal(10, stock.QuantityOnHand);
        Assert.Empty(await transactionRepository.GetAllAsync());
    }

    [Fact]
    public async Task FailedTransactionPersistence_RestoresReservedStock()
    {
        var stock = CreateStock(10);
        var transactionRepository = new MemoryRepository<Transaction>(failSaveOnCall: 2);
        var service = CreateService(stock, transactionRepository: transactionRepository);
        var transaction = CreateTransaction();

        await Assert.ThrowsAsync<IOException>(() =>
            service.CompleteWithRetailItemsAsync(
                transaction,
                [new RetailItemRequest(new Sku("SKU-001"), 3)],
                "Cash"));

        Assert.Equal(10, stock.QuantityOnHand);
        Assert.Empty(await transactionRepository.GetAllAsync());
    }

    [Fact]
    public async Task SuccessfulMultiLineSale_CombinesDuplicateSkusAndPersistsPaidTransaction()
    {
        var stock = CreateStock(10);
        var transactionRepository = new MemoryRepository<Transaction>();
        var service = CreateService(stock, transactionRepository: transactionRepository);
        var transaction = CreateTransaction();

        await service.CompleteWithRetailItemsAsync(
            transaction,
            [
                new RetailItemRequest(new Sku("SKU-001"), 1),
                new RetailItemRequest(new Sku("SKU-001"), 2)
            ],
            "Card");

        Assert.Equal(7, stock.QuantityOnHand);
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Single(transaction.LineItems);
        Assert.Equal(3, transaction.LineItems[0].Quantity);
        Assert.Single(await transactionRepository.GetAllAsync());
    }

    [Fact]
    public async Task RefundRestocksRetailStockExactlyOnce()
    {
        var stock = CreateStock(10);
        var manager = new Staff
        {
            Id = "STF001",
            Name = "Test Manager",
            Email = "manager@example.test",
            Role = StaffRole.BranchManager,
            HomeBranchCode = new BranchCode("B001")
        };
        var service = CreateService(stock, staffRepository: new MemoryRepository<Staff>([manager]));
        var transaction = CreateTransaction();
        await service.CompleteWithRetailItemsAsync(
            transaction,
            [new RetailItemRequest(new Sku("SKU-001"), 3)],
            "Card");

        Assert.Equal(7, stock.QuantityOnHand);

        await service.RefundAsync(transaction.Id, manager.Id, "Customer returned goods");

        Assert.Equal(TransactionStatus.Refunded, transaction.Status);
        Assert.True(transaction.InventoryRestocked);
        Assert.Equal(10, stock.QuantityOnHand);
        Assert.Contains(transaction.Id, stock.RestockedReversalTransactionIds);
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.RefundAsync(transaction.Id, manager.Id, "Duplicate request"));
        Assert.Equal(10, stock.QuantityOnHand);
    }

    [Fact]
    public async Task VoidRestocksRetailStockExactlyOnce()
    {
        var stock = CreateStock(6);
        var manager = new Staff
        {
            Id = "STF001",
            Name = "Test Manager",
            Email = "manager@example.test",
            Role = StaffRole.BranchManager,
            HomeBranchCode = new BranchCode("B001")
        };
        var service = CreateService(stock, staffRepository: new MemoryRepository<Staff>([manager]));
        var transaction = CreateTransaction();
        await service.CompleteWithRetailItemsAsync(
            transaction,
            [new RetailItemRequest(new Sku("SKU-001"), 2)],
            "Cash");
        await service.VoidAsync(transaction.Id, manager.Id, "Duplicate sale");

        Assert.Equal(TransactionStatus.Voided, transaction.Status);
        Assert.True(transaction.InventoryRestocked);
        Assert.Equal(6, stock.QuantityOnHand);
    }

    [Fact]
    public async Task StockAdjustment_IsManagerScopedAndCannotOverdraw()
    {
        var stock = CreateStock(10);
        var manager = new Staff
        {
            Id = "STF001",
            Name = "Test Manager",
            Email = "manager@example.test",
            Role = StaffRole.BranchManager,
            HomeBranchCode = new BranchCode("B001")
        };
        var clerk = new Staff
        {
            Id = "STF002",
            Name = "Test Clerk",
            Email = "clerk@example.test",
            Role = StaffRole.CounterClerk,
            HomeBranchCode = new BranchCode("B001")
        };
        var inventory = new InventoryService(
            new MemoryRepository<Staff>([manager, clerk]),
            new MemoryRepository<BranchStock>([stock]),
            new StubEventDispatcher());

        var updated = await inventory.AdjustStockAsync(
            manager.Id,
            new BranchCode("B001"),
            new Sku("SKU-001"),
            -3,
            StockAdjustmentReason.Damage);

        Assert.Equal(7, updated.QuantityOnHand);
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            inventory.AdjustStockAsync(
                manager.Id,
                new BranchCode("B001"),
                new Sku("SKU-001"),
                -8,
                StockAdjustmentReason.Damage));
        await Assert.ThrowsAsync<UnauthorizedDomainActionException>(() =>
            inventory.AdjustStockAsync(
                manager.Id,
                new BranchCode("B002"),
                new Sku("SKU-001"),
                1,
                StockAdjustmentReason.Replenishment));
        await Assert.ThrowsAsync<UnauthorizedDomainActionException>(() =>
            inventory.AdjustStockAsync(
                clerk.Id,
                new BranchCode("B001"),
                new Sku("SKU-001"),
                1,
                StockAdjustmentReason.Replenishment));
        Assert.Equal(7, stock.QuantityOnHand);
    }

    private static TransactionService CreateService(
        BranchStock stock,
        MemoryRepository<Transaction>? transactionRepository = null,
        MemoryRepository<Staff>? staffRepository = null)
    {
        var product = new Product
        {
            Id = new Sku("SKU-001"),
            Name = "Shipping box",
            Category = "Packaging",
            UnitPrice = new Money(5m),
            TaxCategory = TaxCategory.StandardGst
        };

        return new TransactionService(
            transactionRepository ?? new MemoryRepository<Transaction>(),
            new MemoryRepository<Branch>(
            [
                new Branch
                {
                    Id = new BranchCode("B001"),
                    Name = "Test branch",
                    Region = "NSW",
                    TimeZoneId = "UTC",
                    OpenTime = TimeSpan.Zero,
                    CloseTime = TimeSpan.FromDays(1),
                    CounterTerminals = ["C01"]
                }
            ]),
            staffRepository ?? new MemoryRepository<Staff>(
            [
                new Staff
                {
                    Id = "STF001",
                    Name = "Test Clerk",
                    Email = "clerk@example.test",
                    Role = StaffRole.CounterClerk,
                    HomeBranchCode = new BranchCode("B001")
                }
            ]),
            new MemoryRepository<BranchStock>([stock]),
            new StubCourierRateService(),
            new StubCatalogueService(product),
            new StubIdentityVerificationService(),
            new StubEventDispatcher(),
            new TransactionRules(new Money(1000m), new Money(20000m)));
    }

    private static BranchStock CreateStock(int quantity)
    {
        var stock = new BranchStock
        {
            Id = Guid.NewGuid(),
            BranchCode = new BranchCode("B001"),
            ProductSku = new Sku("SKU-001")
        };
        stock.AddStock(quantity);
        return stock;
    }

    private static Transaction CreateTransaction() => new()
    {
        Id = Guid.NewGuid(),
        BranchCode = new BranchCode("B001"),
        CounterId = "C01",
        ClerkStaffId = "STF001"
    };

    private sealed class MemoryRepository<T> : IRepository<T> where T : class
    {
        private readonly List<T> _items;
        private readonly int _failSaveOnCall;
        private int _saveCalls;

        public MemoryRepository(IEnumerable<T>? items = null, int failSaveOnCall = 0)
        {
            _items = items?.ToList() ?? [];
            _failSaveOnCall = failSaveOnCall;
        }

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<T>>(_items);

        public Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        {
            var idProperty = typeof(T).GetProperty("Id")
                ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property.");
            return Task.FromResult(_items.FirstOrDefault(item => Equals(idProperty.GetValue(item), id)));
        }

        public Task<T?> UpdateAsync(object id, Action<T> update, CancellationToken ct = default)
        {
            var idProperty = typeof(T).GetProperty("Id")
                ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property.");
            var item = _items.FirstOrDefault(value => Equals(idProperty.GetValue(value), id));
            if (item is not null)
            {
                update(item);
            }

            return Task.FromResult(item);
        }

        public Task SaveAsync(T entity, CancellationToken ct = default)
        {
            if (++_saveCalls == _failSaveOnCall)
            {
                throw new IOException("Simulated transaction persistence failure.");
            }

            var idProperty = typeof(T).GetProperty("Id");
            var id = idProperty?.GetValue(entity);
            var existingIndex = idProperty is null
                ? -1
                : _items.FindIndex(item => Equals(idProperty.GetValue(item), id));
            if (existingIndex >= 0)
            {
                _items[existingIndex] = entity;
            }
            else
            {
                _items.Add(entity);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(object id, CancellationToken ct = default)
        {
            var idProperty = typeof(T).GetProperty("Id")
                ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property.");
            _items.RemoveAll(item => Equals(idProperty.GetValue(item), id));
            return Task.CompletedTask;
        }
    }

    private sealed class StubCatalogueService(Product product) : ICatalogueService
    {
        public ValueTask<IReadOnlyList<Product>> GetProductsAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<Product>>([product]);

        public ValueTask<Product?> GetProductBySkuAsync(Sku sku, CancellationToken ct = default) =>
            ValueTask.FromResult<Product?>(product.Id == sku ? product : null);
    }

    private sealed class StubCourierRateService : ICourierRateService
    {
        public Task<Money> GetEstimatedCourierRateAsync(ShippingDetails details, CancellationToken ct = default) =>
            Task.FromResult(new Money(10m));
    }

    private sealed class StubIdentityVerificationService : IIdentityVerificationService
    {
        public Task<IdVerificationDetails> VerifyAsync(
            DocumentType documentType,
            string documentNumber,
            CancellationToken ct = default) =>
            Task.FromResult(IdVerificationDetails.FromDocumentNumber(documentType, documentNumber, true));
    }

    private sealed class StubEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default) => Task.CompletedTask;
    }
}
