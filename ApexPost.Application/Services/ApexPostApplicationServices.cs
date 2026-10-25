using ApexPost.Application.Interfaces;
using ApexPost.Application.Pricing;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Events;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed record DashboardSnapshot(
    IReadOnlyList<Branch> Branches,
    IReadOnlyList<Product> Products,
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<BranchStock> Inventory,
    IReadOnlyList<string> Alerts);

public sealed record TransactionRules(
    Money HighValueMoneyOrderThreshold,
    Money MaximumMoneyOrderAmount);
public sealed record RetailItemRequest(Sku Sku, int Quantity);
public sealed record CounterCheckoutRequest(
    string CounterId,
    IReadOnlyCollection<RetailItemRequest> RetailItems,
    ShippingDetails? Shipping,
    decimal? MoneyOrderAmount,
    string? Sender,
    string? Recipient,
    bool IncludeIdentityVerification,
    DocumentType DocumentType,
    string? DocumentNumber,
    string PaymentMethod);

public sealed record StaffRecordInput(
    string Name,
    string Email,
    StaffRole Role,
    string HomeBranchCode);

public sealed class StaffManagementService
{
    private readonly IRepository<Staff> _staff;
    private readonly IRepository<Branch> _branches;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public StaffManagementService(IRepository<Staff> staff, IRepository<Branch> branches)
    {
        _staff = staff;
        _branches = branches;
    }

    public async Task<IReadOnlyList<Staff>> GetAllAsync(CancellationToken ct = default) =>
        (await _staff.GetAllAsync(ct))
        .OrderBy(person => person.HomeBranchCode.Value)
        .ThenBy(person => person.Name)
        .ToList();

    public Task<Staff?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _staff.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Branch>> GetBranchesAsync(CancellationToken ct = default) =>
        _branches.GetAllAsync(ct);

    public async Task<Staff> CreateAsync(StaffRecordInput input, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            var normalized = NormalizeAndValidate(input);
            var staff = await _staff.GetAllAsync(ct);
            EnsureEmailIsUnique(staff, normalized.Email, exceptId: null);
            await EnsureBranchExistsAsync(normalized.HomeBranchCode, ct);

            var nextId = staff
                .Select(person => person.Id.StartsWith("STF", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(person.Id.AsSpan(3), out var number) ? number : 0)
                .DefaultIfEmpty()
                .Max() + 1;
            var created = new Staff
            {
                Id = $"STF{nextId:D3}",
                Name = normalized.Name,
                Email = normalized.Email,
                Role = normalized.Role,
                HomeBranchCode = new BranchCode(normalized.HomeBranchCode)
            };
            await _staff.SaveAsync(created, ct);
            return created;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Staff> UpdateAsync(
        string id,
        StaffRecordInput input,
        CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            var existing = await _staff.GetByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Staff member {id} was not found.");
            var normalized = NormalizeAndValidate(input);
            var staff = await _staff.GetAllAsync(ct);
            EnsureEmailIsUnique(staff, normalized.Email, id);
            await EnsureBranchExistsAsync(normalized.HomeBranchCode, ct);

            var updated = new Staff
            {
                Id = existing.Id,
                Name = normalized.Name,
                Email = normalized.Email,
                Role = normalized.Role,
                HomeBranchCode = new BranchCode(normalized.HomeBranchCode),
                IsActive = existing.IsActive
            };
            await _staff.SaveAsync(updated, ct);
            return updated;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task DeactivateAsync(string id, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            var existing = await _staff.GetByIdAsync(id, ct)
                ?? throw new KeyNotFoundException($"Staff member {id} was not found.");
            existing.IsActive = false;
            await _staff.SaveAsync(existing, ct);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task EnsureBranchExistsAsync(string branchCode, CancellationToken ct)
    {
        var branches = await _branches.GetAllAsync(ct);
        if (!branches.Any(branch => branch.Id.Value == branchCode))
        {
            throw new DomainValidationException("Select an existing branch.");
        }
    }

    private static (string Name, string Email, StaffRole Role, string HomeBranchCode) NormalizeAndValidate(
        StaffRecordInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var name = input.Name.Trim();
        var email = input.Email.Trim();
        var branchCode = input.HomeBranchCode.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(branchCode))
        {
            throw new DomainValidationException("Name, email, and home branch are required.");
        }

        if (!Enum.IsDefined(input.Role))
        {
            throw new DomainValidationException("Select a valid staff role.");
        }

        return (name, email, input.Role, branchCode);
    }

    private static void EnsureEmailIsUnique(IEnumerable<Staff> staff, string email, string? exceptId)
    {
        if (staff.Any(person =>
            person.Id != exceptId && person.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainValidationException("A staff member with this email already exists.");
        }
    }
}

public sealed class InventoryService
{
    private readonly IRepository<Staff> _staff;
    private readonly IRepository<BranchStock> _stocks;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public InventoryService(
        IRepository<Staff> staff,
        IRepository<BranchStock> stocks,
        IDomainEventDispatcher eventDispatcher)
    {
        _staff = staff;
        _stocks = stocks;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<BranchStock> AdjustStockAsync(
        string managerId,
        BranchCode branchCode,
        Sku sku,
        int quantityDelta,
        StockAdjustmentReason reason,
        CancellationToken ct = default)
    {
        if (quantityDelta == 0 || quantityDelta == int.MinValue)
        {
            throw new DomainValidationException("The stock adjustment quantity must be non-zero and in range.");
        }

        if (!Enum.IsDefined(reason))
        {
            throw new DomainValidationException("A valid stock adjustment reason is required.");
        }

        var manager = await _staff.GetByIdAsync(managerId, ct)
            ?? throw new UnauthorizedDomainActionException("The signed-in staff member is not available.");
        if (!manager.IsActive
            || manager.Role != StaffRole.BranchManager
            || manager.HomeBranchCode != branchCode)
        {
            throw new UnauthorizedDomainActionException("An active Branch Manager assigned to this branch is required.");
        }

        var stock = (await _stocks.GetAllAsync(ct)).FirstOrDefault(item =>
            item.BranchCode == branchCode && item.ProductSku == sku)
            ?? throw new KeyNotFoundException($"Product {sku.Value} is not stocked at branch {branchCode.Value}.");
        var updated = await _stocks.UpdateAsync(stock.Id, item => item.ApplyAdjustment(quantityDelta), ct)
            ?? throw new KeyNotFoundException($"Stock record for {sku.Value} is no longer available.");

        if (updated.QuantityOnHand <= 5)
        {
            await _eventDispatcher.DispatchAsync(
                new StockLowEvent(branchCode, sku, updated.QuantityOnHand),
                ct);
        }

        return updated;
    }
}

public sealed class DashboardService
{
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Transaction> _transactions;
    private readonly IRepository<BranchStock> _stocks;

    public DashboardService(
        IRepository<Branch> branches,
        IRepository<Product> products,
        IRepository<Transaction> transactions,
        IRepository<BranchStock> stocks)
    {
        _branches = branches;
        _products = products;
        _transactions = transactions;
        _stocks = stocks;
    }

    public async Task<DashboardSnapshot> BuildAsync(string? branchCode = null, CancellationToken ct = default)
    {
        var branchesTask = _branches.GetAllAsync(ct);
        var productsTask = _products.GetAllAsync(ct);
        var transactionsTask = _transactions.GetAllAsync(ct);
        var stockTask = _stocks.GetAllAsync(ct);

        await Task.WhenAll(branchesTask, productsTask, transactionsTask, stockTask);

        var branches = await branchesTask;
        var products = await productsTask;
        var transactions = await transactionsTask;
        var stock = await stockTask;

        if (branchCode is not null)
        {
            branches = branches.Where(branch => branch.Id.Value == branchCode).ToList();
            transactions = transactions.Where(transaction => transaction.BranchCode.Value == branchCode).ToList();
            stock = stock.Where(item => item.BranchCode.Value == branchCode).ToList();
        }

        var alerts = new List<string>();
        foreach (var branch in branches)
        {
            if (!branch.IsActive)
            {
                alerts.Add($"Branch {branch.Id.Value} is inactive.");
            }

            foreach (var item in stock.Where(x => x.BranchCode == branch.Id))
            {
                if (item.QuantityOnHand <= 5)
                {
                    alerts.Add($"Low stock: {item.ProductSku.Value} in {branch.Id.Value}.");
                }
            }
        }

        return new DashboardSnapshot(branches, products, transactions, stock, alerts);
    }
}

public sealed class ReportingService
{
    private readonly IRepository<Transaction> _transactions;
    private readonly IRepository<BranchStock> _stocks;
    private readonly IRepository<Branch> _branches;

    public ReportingService(
        IRepository<Transaction> transactions,
        IRepository<BranchStock> stocks,
        IRepository<Branch> branches)
    {
        _transactions = transactions;
        _stocks = stocks;
        _branches = branches;
    }

    public async Task<IReadOnlyList<(string Branch, Money Sales, Money Services)>> GetDailyBranchSalesAsync(
        string? branchCode = null,
        CancellationToken ct = default)
    {
        var transactionsTask = _transactions.GetAllAsync(ct);
        var branchesTask = _branches.GetAllAsync(ct);
        await Task.WhenAll(transactionsTask, branchesTask);
        var transactions = await transactionsTask;
        var branchTimeZones = (await branchesTask).ToDictionary(
            branch => branch.Id.Value,
            branch => TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId),
            StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        var todayByBranch = branchTimeZones.ToDictionary(
            entry => entry.Key,
            entry => TimeZoneInfo.ConvertTime(now, entry.Value).Date,
            StringComparer.Ordinal);
        var paidTransactions = transactions
            .Where(transaction => transaction.Status == TransactionStatus.Paid
                && (branchCode is null || transaction.BranchCode.Value == branchCode))
            .ToList();
        foreach (var transaction in paidTransactions)
        {
            if (!branchTimeZones.ContainsKey(transaction.BranchCode.Value))
            {
                throw new KeyNotFoundException($"Branch {transaction.BranchCode.Value} has no timezone configuration.");
            }
        }

        var groups = transactions
            .Where(transaction => transaction.Status == TransactionStatus.Paid
                && (branchCode is null || transaction.BranchCode.Value == branchCode)
                && TimeZoneInfo.ConvertTime(transaction.CreatedAt, branchTimeZones[transaction.BranchCode.Value]).Date
                    == todayByBranch[transaction.BranchCode.Value])
            .GroupBy(x => x.BranchCode.Value)
            .Select(g =>
            {
                var sales = g.Aggregate(Money.Zero, (running, x) => running + x.LineItems
                    .Where(i => i.ItemType == LineItemType.RetailProduct)
                    .Aggregate(Money.Zero, (acc, item) => acc + item.GrandTotal));

                var services = g.Aggregate(Money.Zero, (running, x) => running + x.LineItems
                    .Where(i => i.ItemType != LineItemType.RetailProduct)
                    .Aggregate(Money.Zero, (acc, item) => acc + item.GrandTotal));

                return (g.Key, sales, services);
            })
            .ToList();

        return groups;
    }

    public async Task<IReadOnlyList<(string Branch, string Sku, int Quantity)>> GetLowStockReportAsync(
        int threshold,
        string? branchCode = null,
        CancellationToken ct = default,
        Predicate<BranchStock>? specification = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        var stock = await _stocks.GetAllAsync(ct);
        return stock
            .Where(x => (specification?.Invoke(x) ?? x.QuantityOnHand <= threshold)
                && (branchCode is null || x.BranchCode.Value == branchCode))
            .Select(x => (x.BranchCode.Value, x.ProductSku.Value, x.QuantityOnHand))
            .ToList();
    }

    public async Task<IReadOnlyList<(string Zone, ServiceLevel ServiceLevel, int Count)>> GetShippingVolumeByZoneAsync(
        string? branchCode = null,
        CancellationToken ct = default)
    {
        var transactions = await _transactions.GetAllAsync(ct);
        return transactions
            .Where(transaction => branchCode is null || transaction.BranchCode.Value == branchCode)
            .Where(transaction => transaction.Status == TransactionStatus.Paid)
            .SelectMany(transaction => transaction.LineItems)
            .Where(line => line.Shipping is not null)
            .GroupBy(line => (line.Shipping!.DestinationZone, line.Shipping.ServiceLevel))
            .OrderBy(group => group.Key.DestinationZone)
            .ThenBy(group => group.Key.ServiceLevel)
            .Select(group => (group.Key.DestinationZone, group.Key.ServiceLevel, group.Sum(line => line.Quantity)))
            .ToList();
    }

    public async Task<IReadOnlyList<(string Branch, int MoneyOrders, int IdentityVerifications)>> GetMoneyOrderAuditReportAsync(
        string? branchCode = null,
        CancellationToken ct = default)
    {
        var transactions = await _transactions.GetAllAsync(ct);
        return transactions
            .Where(transaction => branchCode is null || transaction.BranchCode.Value == branchCode)
            .SelectMany(transaction => transaction.LineItems.Select(line => (transaction.BranchCode.Value, line.ItemType)))
            .Where(item => item.ItemType is LineItemType.MoneyOrder or LineItemType.IdentityVerification)
            .GroupBy(item => item.Value)
            .OrderBy(group => group.Key)
            .Select(group => (
                group.Key,
                group.Count(item => item.ItemType == LineItemType.MoneyOrder),
                group.Count(item => item.ItemType == LineItemType.IdentityVerification)))
            .ToList();
    }
}

public sealed class TransactionService
{
    private readonly IRepository<Transaction> _transactions;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Staff> _staff;
    private readonly IRepository<BranchStock> _stocks;
    private readonly ICourierRateService _courierRates;
    private readonly ICatalogueService _catalogue;
    private readonly IIdentityVerificationService _identityVerification;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly TransactionRules _rules;
    public Money HighValueMoneyOrderThreshold => _rules.HighValueMoneyOrderThreshold;

    public TransactionService(
        IRepository<Transaction> transactions,
        IRepository<Branch> branches,
        IRepository<Staff> staff,
        IRepository<BranchStock> stocks,
        ICourierRateService courierRates,
        ICatalogueService catalogue,
        IIdentityVerificationService identityVerification,
        IDomainEventDispatcher eventDispatcher,
        TransactionRules rules)
    {
        _transactions = transactions;
        _branches = branches;
        _staff = staff;
        _stocks = stocks;
        _courierRates = courierRates;
        _catalogue = catalogue;
        _identityVerification = identityVerification;
        _eventDispatcher = eventDispatcher;
        _rules = rules;
    }

    public async Task<Transaction> CheckoutAsync(
        string branchCode,
        string clerkId,
        CounterCheckoutRequest request,
        Guid transactionId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RetailItems.Count > 20)
        {
            throw new DomainValidationException("A basket can contain at most 20 distinct retail items.");
        }

        var moneyOrderAmount = request.MoneyOrderAmount ?? 0m;
        if (moneyOrderAmount < 0 || moneyOrderAmount > _rules.MaximumMoneyOrderAmount.Amount)
        {
            throw new DomainValidationException(
                $"Money order amount must be between zero and {_rules.MaximumMoneyOrderAmount.Amount}.");
        }

        if (moneyOrderAmount > 0
            && (string.IsNullOrWhiteSpace(request.Sender) || string.IsNullOrWhiteSpace(request.Recipient)))
        {
            throw new DomainValidationException("Sender and recipient are required for a money order.");
        }

        if (moneyOrderAmount > _rules.HighValueMoneyOrderThreshold.Amount
            && !request.IncludeIdentityVerification)
        {
            throw new DomainValidationException(
                $"Money orders above {_rules.HighValueMoneyOrderThreshold.Amount:C} require identity verification in this transaction.");
        }

        if (request.IncludeIdentityVerification && string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            throw new DomainValidationException("A document number is required to perform identity verification.");
        }

        if (request.RetailItems.Count == 0
            && request.Shipping is null
            && moneyOrderAmount == 0
            && !request.IncludeIdentityVerification)
        {
            throw new DomainValidationException("Add at least one retail item or service to the transaction.");
        }

        var transaction = await CreateAsync(branchCode, request.CounterId, clerkId, ct, transactionId);
        if (request.Shipping is not null)
        {
            await AddShippingAsync(transaction, request.Shipping, ct);
        }

        if (moneyOrderAmount > 0)
        {
            AddMoneyOrder(
                transaction,
                new Money(moneyOrderAmount),
                request.Sender!,
                request.Recipient!);
        }

        if (request.IncludeIdentityVerification)
        {
            var verification = await _identityVerification.VerifyAsync(
                request.DocumentType,
                request.DocumentNumber!,
                ct);
            AddIdentityVerification(transaction, verification);
        }

        await CompleteWithRetailItemsAsync(
            transaction,
            request.RetailItems,
            request.PaymentMethod,
            ct);
        return transaction;
    }

    public async Task<Transaction> CreateAsync(
        string branchCode,
        string counterId,
        string clerkId,
        CancellationToken ct = default,
        Guid? transactionId = null)
    {
        var branchesTask = _branches.GetAllAsync(ct);
        var staffTask = _staff.GetAllAsync(ct);
        await Task.WhenAll(branchesTask, staffTask);
        var branches = await branchesTask;
        var staffMembers = await staffTask;
        var selectedBranch = branches.FirstOrDefault(x => x.Id.Value == branchCode) ?? throw new KeyNotFoundException("Branch not found.");
        var clerk = staffMembers.FirstOrDefault(x => x.Id == clerkId) ?? throw new UnauthorizedDomainActionException("Staff not found.");

        clerk.EnsureCanTransact(selectedBranch.Id);
        selectedBranch.EnsureAcceptsTransaction(DateTimeOffset.UtcNow);
        if (!selectedBranch.CounterTerminals.Contains(counterId, StringComparer.Ordinal))
        {
            throw new DomainValidationException($"Counter {counterId} does not belong to branch {branchCode}.");
        }

        return new Transaction
        {
            Id = transactionId ?? Guid.NewGuid(),
            BranchCode = selectedBranch.Id,
            CounterId = counterId,
            ClerkStaffId = clerkId
        };
    }

    public async Task CompleteWithRetailItemsAsync(
        Transaction transaction,
        IReadOnlyCollection<RetailItemRequest> retailItems,
        string paymentMethod,
        CancellationToken ct = default)
    {
        if (transaction.Status != TransactionStatus.Open)
        {
            throw new DomainValidationException("Cannot complete a finalized transaction.");
        }

        ArgumentNullException.ThrowIfNull(retailItems);
        var requestedItems = retailItems
            .GroupBy(item => item.Sku)
            .Select(group => new RetailItemRequest(group.Key, group.Sum(item => item.Quantity)))
            .ToList();
        if (requestedItems.Any(item => item.Quantity <= 0))
        {
            throw new DomainValidationException("Retail item quantities must be greater than zero.");
        }

        var branchTask = _branches.GetByIdAsync(transaction.BranchCode, ct);
        var clerkTask = _staff.GetByIdAsync(transaction.ClerkStaffId, ct);
        await Task.WhenAll(branchTask, clerkTask);
        var branch = await branchTask ?? throw new KeyNotFoundException("Branch not found.");
        var clerk = await clerkTask ?? throw new UnauthorizedDomainActionException("Staff not found.");
        clerk.EnsureCanTransact(branch.Id);
        branch.EnsureAcceptsTransaction(DateTimeOffset.UtcNow);
        if (!branch.CounterTerminals.Contains(transaction.CounterId, StringComparer.Ordinal))
        {
            throw new DomainValidationException($"Counter {transaction.CounterId} does not belong to branch {branch.Id.Value}.");
        }

        var stock = await _stocks.GetAllAsync(ct);
        var reservations = new List<(Guid StockId, int Quantity)>();
        var openTransactionPersisted = false;
        var paidTransactionPersisted = false;
        try
        {
            await _transactions.SaveAsync(transaction, ct);
            openTransactionPersisted = true;

            foreach (var requestedItem in requestedItems)
            {
                var product = await _catalogue.GetProductBySkuAsync(requestedItem.Sku, ct)
                    ?? throw new KeyNotFoundException($"Product {requestedItem.Sku.Value} was not found.");
                if (product.Category.ToUpperInvariant() is not ("PACKAGING" or "STATIONERY" or "OFFICE SUPPLIES"))
                {
                    throw new DomainValidationException($"Product {product.Id.Value} has an unsupported category.");
                }

                var branchStockId = stock.FirstOrDefault(item =>
                    item.BranchCode == transaction.BranchCode && item.ProductSku == requestedItem.Sku)?.Id;
                if (branchStockId is null)
                {
                    throw new DomainValidationException($"Product {requestedItem.Sku.Value} is not stocked at this branch.");
                }

                var branchStock = await _stocks.UpdateAsync(
                    branchStockId.Value,
                    item => item.ReduceStock(requestedItem.Quantity),
                    ct) ?? throw new DomainValidationException("The selected product is no longer stocked at this branch.");
                reservations.Add((branchStockId.Value, requestedItem.Quantity));
                transaction.AddLineItem(new TransactionLineItem(
                    Guid.NewGuid(),
                    product.Name,
                    LineItemType.RetailProduct,
                    requestedItem.Quantity,
                    product.UnitPrice,
                    product.TaxCategory,
                    requestedItem.Sku));
                if (branchStock.QuantityOnHand <= 5)
                {
                    transaction.AddDomainEvent(new StockLowEvent(transaction.BranchCode, requestedItem.Sku, branchStock.QuantityOnHand));
                }
            }

            transaction.Complete(paymentMethod, _rules.HighValueMoneyOrderThreshold);
            await _transactions.SaveAsync(transaction, ct);
            paidTransactionPersisted = true;

            await DispatchDomainEventsAsync(transaction, ct);
        }
        catch (Exception completionFailure) when (!paidTransactionPersisted)
        {
            try
            {
                foreach (var reservation in reservations.AsEnumerable().Reverse())
                {
                    var restoredStock = await _stocks.UpdateAsync(
                        reservation.StockId,
                        item => item.AddStock(reservation.Quantity),
                        CancellationToken.None);
                    if (restoredStock is null)
                    {
                        throw new InvalidOperationException($"Stock record {reservation.StockId} disappeared during transaction rollback.");
                    }
                }

                if (openTransactionPersisted)
                {
                    await _transactions.DeleteAsync(transaction.Id, CancellationToken.None);
                }
            }
            catch (Exception rollbackFailure)
            {
                throw new AggregateException(
                    "Transaction completion failed and the inventory rollback also failed; reconcile the affected branch stock.",
                    completionFailure,
                    rollbackFailure);
            }

            throw;
        }
    }

    public async Task AddShippingAsync(Transaction transaction, ShippingDetails shippingDetails, CancellationToken ct = default)
    {
        var rate = await _courierRates.GetEstimatedCourierRateAsync(shippingDetails, ct);
        var lineItem = new TransactionLineItem(Guid.NewGuid(), $"Shipping: {shippingDetails.ServiceLevel}", LineItemType.ParcelShipping, 1, rate, TaxCategory.Exempt, Shipping: shippingDetails);
        transaction.AddLineItem(lineItem);
    }

    public void AddMoneyOrder(Transaction transaction, Money amount, string sender, string recipient)
    {
        if (amount.Amount <= 0 || string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(recipient))
        {
            throw new DomainValidationException("A positive money order amount, sender, and recipient are required.");
        }

        var fee = new Money(10m, amount.Currency);
        transaction.AddLineItem(new TransactionLineItem(
            Guid.NewGuid(),
            "Money order",
            LineItemType.MoneyOrder,
            1,
            amount + fee,
            TaxCategory.Exempt,
            MoneyOrder: new MoneyOrderDetails(amount, sender.Trim(), recipient.Trim(), fee)));
    }

    public void AddIdentityVerification(Transaction transaction, IdVerificationDetails verification)
    {
        transaction.AddLineItem(new TransactionLineItem(
            Guid.NewGuid(),
            "Identity verification",
            LineItemType.IdentityVerification,
            1,
            Money.Zero,
            TaxCategory.Exempt,
            IdVerification: verification));
    }

    public async Task VoidAsync(Guid transactionId, string managerId, string reason, CancellationToken ct = default)
    {
        await ReverseTransactionAsync(transactionId, managerId, reason, refund: false, ct);
    }

    public async Task RefundAsync(Guid transactionId, string managerId, string reason, CancellationToken ct = default)
    {
        await ReverseTransactionAsync(transactionId, managerId, reason, refund: true, ct);
    }

    private async Task ReverseTransactionAsync(
        Guid transactionId,
        string managerId,
        string reason,
        bool refund,
        CancellationToken ct)
    {
        var transaction = await _transactions.GetByIdAsync(transactionId, ct)
            ?? throw new KeyNotFoundException("Transaction not found.");
        var manager = await _staff.GetByIdAsync(managerId, ct)
            ?? throw new UnauthorizedDomainActionException("The signed-in staff member is not available.");
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("A reason is required for a transaction reversal.");
        }

        var targetStatus = refund ? TransactionStatus.Refunded : TransactionStatus.Voided;
        var wasResuming = transaction.Status == targetStatus && !transaction.InventoryRestocked;
        if (transaction.Status == TransactionStatus.Paid)
        {
            if (refund)
            {
                transaction.Refund(manager, reason);
            }
            else
            {
                transaction.Void(manager, reason);
            }

            await _transactions.SaveAsync(transaction, ct);
        }
        else if (!wasResuming)
        {
            var action = refund ? "refunded" : "voided";
            throw new DomainValidationException($"Only paid transactions or {action} transactions awaiting inventory restock can be reversed.");
        }
        else if (!manager.IsActive
            || manager.Role != StaffRole.BranchManager
            || manager.HomeBranchCode != transaction.BranchCode)
        {
            throw new UnauthorizedDomainActionException("An active Branch Manager assigned to this branch is required.");
        }

        if (wasResuming && transaction.DomainEvents.Count == 0
            && transaction.VoidReason is not null
            && transaction.VoidedByManagerId is not null)
        {
            transaction.AddDomainEvent(new TransactionVoidedEvent(
                transaction.Id,
                transaction.BranchCode,
                transaction.VoidedByManagerId,
                transaction.VoidReason));
        }

        await RestoreRetailInventoryAsync(transaction, ct);
        transaction.MarkReversalInventoryRestocked();
        await _transactions.SaveAsync(transaction, ct);
        await DispatchDomainEventsAsync(transaction, ct);
    }

    private async Task RestoreRetailInventoryAsync(Transaction transaction, CancellationToken ct)
    {
        var retailQuantities = transaction.LineItems
            .Where(item => item.ItemType == LineItemType.RetailProduct && item.ProductSku is not null)
            .GroupBy(item => item.ProductSku!.Value)
            .Select(group => (Sku: group.Key, Quantity: group.Sum(item => item.Quantity)))
            .ToList();
        var stock = await _stocks.GetAllAsync(ct);
        foreach (var item in retailQuantities)
        {
            var stockId = stock.FirstOrDefault(record =>
                record.BranchCode == transaction.BranchCode && record.ProductSku == item.Sku)?.Id
                ?? throw new KeyNotFoundException($"Stock for reversed product {item.Sku.Value} was not found.");
            var updated = await _stocks.UpdateAsync(
                stockId,
                record => record.RestockForReversal(transaction.Id, item.Quantity),
                ct);
            if (updated is null)
            {
                throw new KeyNotFoundException($"Stock for reversed product {item.Sku.Value} is no longer available.");
            }
        }
    }

    private async Task DispatchDomainEventsAsync(Transaction transaction, CancellationToken ct)
    {
        foreach (var domainEvent in transaction.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, ct);
        }

        transaction.ClearDomainEvents();
    }
}
