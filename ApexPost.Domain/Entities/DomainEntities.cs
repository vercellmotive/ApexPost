using ApexPost.Domain.Common;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Events;
using ApexPost.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Domain.Entities;

public class Branch : AggregateRoot<BranchCode>
{
    public required string Name { get; init; }
    public required string Region { get; init; }
    public required TimeSpan OpenTime { get; init; }
    public required TimeSpan CloseTime { get; init; }
    public string TimeZoneId { get; init; } = "Australia/Sydney";
    public bool IsActive { get; set; } = true;
    public List<string> CounterTerminals { get; init; } = [];

    public bool IsOpen(DateTimeOffset currentTime)
    {
        if (!IsActive) return false;
        var time = TimeZoneInfo.ConvertTime(currentTime, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)).TimeOfDay;
        return time >= OpenTime && time <= CloseTime;
    }

    public void EnsureAcceptsTransaction(DateTimeOffset currentTime)
    {
        if (!IsOpen(currentTime))
        {
            throw new DomainValidationException($"Branch {Id.Value} is closed or inactive. Transactions are forbidden.");
        }
    }
}

public class BranchStock : Entity<Guid>
{
    public required BranchCode BranchCode { get; init; }
    public required Sku ProductSku { get; init; }
    [System.Text.Json.Serialization.JsonInclude]
    public int QuantityOnHand { get; private set; }
    [System.Text.Json.Serialization.JsonInclude]
    public HashSet<Guid> RestockedReversalTransactionIds { get; private set; } = [];

    public void AddStock(int amount)
    {
        if (amount < 0) throw new DomainValidationException("Stock adjustments cannot be negative.");
        QuantityOnHand += amount;
    }

    public void ReduceStock(int quantity)
    {
        if (quantity <= 0) throw new DomainValidationException("Quantity to reduce must be positive.");
        if (QuantityOnHand - quantity < 0) throw new DomainValidationException("Branch stock cannot go negative.");
        QuantityOnHand -= quantity;
    }

    public void ApplyAdjustment(int quantityDelta)
    {
        if (quantityDelta == 0)
        {
            throw new DomainValidationException("A stock adjustment must change inventory.");
        }

        if (quantityDelta > 0)
        {
            AddStock(quantityDelta);
            return;
        }

        if (quantityDelta == int.MinValue)
        {
            throw new DomainValidationException("The stock adjustment quantity is out of range.");
        }

        ReduceStock(-quantityDelta);
    }

    public void RestockForReversal(Guid transactionId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainValidationException("Transaction reversal restock quantity must be positive.");
        }

        if (RestockedReversalTransactionIds.Add(transactionId))
        {
            AddStock(quantity);
        }
    }
}

public class Staff : Entity<string>
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required StaffRole Role { get; init; }
    public required BranchCode HomeBranchCode { get; init; }
    public bool IsActive { get; set; } = true;
    public string? PasswordHash { get; init; }

    public void EnsureCanTransact(BranchCode currentBranch)
    {
        if(!IsActive) throw new UnauthorizedDomainActionException($"Staff {Id} is deactivated.");
        if (Role is not (StaffRole.CounterClerk or StaffRole.BranchManager))
        {
            throw new UnauthorizedDomainActionException($"Staff role {Role} cannot process counter transactions.");
        }

        if(HomeBranchCode != currentBranch) throw new UnauthorizedDomainActionException($"Staff member {Id} belongs to branch {HomeBranchCode.Value}, not {currentBranch.Value}");
    }
}

public static class ProductCategories
{
    public const string Packaging = "Packaging";
    public const string Stationery = "Stationery";
    public const string OfficeSupplies = "Office Supplies";

    public static IReadOnlyList<string> All { get; } = [Packaging, Stationery, OfficeSupplies];

    public static bool IsSupported(string category) =>
        All.Contains(category, StringComparer.OrdinalIgnoreCase);
}

public class Product : Entity<Sku>
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required Money UnitPrice { get; init; }
    public required TaxCategory TaxCategory { get; init; }
}

public class ShippingZone : Entity<string>
{
    public required string Name { get; init; }
    public decimal Multiplier { get; init; }
}

public record ShippingDetails(bool IsParcel, Weight Weight, Dimensions Dimensions, string DestinationZone, ServiceLevel ServiceLevel);
public record MoneyOrderDetails(Money Amount, string Sender, string Recipient, Money Fee);
public sealed record IdVerificationDetails
{
    public DocumentType DocType { get; }
    public string MaskedDocNumber { get; }
    public bool IsVerified { get; }

    public IdVerificationDetails(DocumentType docType, string maskedDocNumber, bool isVerified)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(maskedDocNumber);
        if (maskedDocNumber.Length < 8
            || maskedDocNumber[..^4].Any(character => character != '*')
            || maskedDocNumber[^4..].Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("Only a masked document value ending in its last four characters may be stored.", nameof(maskedDocNumber));
        }

        DocType = docType;
        MaskedDocNumber = maskedDocNumber;
        IsVerified = isVerified;
    }

    public static IdVerificationDetails FromDocumentNumber(DocumentType documentType, string documentNumber, bool isVerified)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        if (documentNumber.Length <= 4)
        {
            throw new ArgumentException("The document number must be longer than the four characters that are retained.", nameof(documentNumber));
        }

        var lastFour = documentNumber[^4..];
        return new IdVerificationDetails(documentType, $"******{lastFour}", isVerified);
    }
}

public record TransactionLineItem(
    Guid Id, 
    [property: NotBlank] string Description,
    LineItemType ItemType,
    int Quantity,
    Money UnitPrice,
    TaxCategory TaxCategory,
    Sku? ProductSku = null,
    ShippingDetails? Shipping = null,
    MoneyOrderDetails? MoneyOrder = null,
    IdVerificationDetails? IdVerification = null)
{
    public Money TotalPrice => UnitPrice * Quantity;
    public Money TaxAmount => TotalPrice.CalculateTax(TaxCategory);
    public Money GrandTotal => TotalPrice + TaxAmount;
}

public class Transaction : AggregateRoot<Guid>
{
    private List<TransactionLineItem> _lineItems = [];

    public required BranchCode BranchCode { get; init; }
    public required string CounterId { get; init; }
    public required string ClerkStaffId { get; init; }
    [System.Text.Json.Serialization.JsonInclude]
    public TransactionStatus Status { get; private set; } = TransactionStatus.Open;
    [System.Text.Json.Serialization.JsonInclude]
    public IReadOnlyList<TransactionLineItem> LineItems
    {
        get => _lineItems.AsReadOnly();
        private set => _lineItems = [.. value];
    }
    [System.Text.Json.Serialization.JsonInclude]
    public string PaymentMethod { get; private set; } = "Pending";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    [System.Text.Json.Serialization.JsonInclude]
    public string? VoidReason { get; private set; }
    [System.Text.Json.Serialization.JsonInclude]
    public string? VoidedByManagerId { get; private set; }
    [System.Text.Json.Serialization.JsonInclude]
    public bool InventoryRestocked { get; private set; }

    public Money SubTotal => LineItems.Aggregate(Money.Zero, (acc, item) => acc + item.TotalPrice);
    public Money TotalTax => LineItems.Aggregate(Money.Zero, (acc, item) => acc + item.TaxAmount);
    public Money GrandTotal => SubTotal + TotalTax;

    public void AddLineItem(TransactionLineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        DomainAttributeValidator.Validate(item);
        if (Status != TransactionStatus.Open) throw new DomainValidationException($"Cannot modify a finalized or voided transaction.");
        if (item.Quantity <= 0) throw new DomainValidationException("Line item quantity must be positive.");
        if (!Enum.IsDefined(item.TaxCategory))
        {
            throw new DomainValidationException("The line item tax category is not supported.");
        }

        var isValid = item.ItemType switch
        {
            LineItemType.RetailProduct => item.ProductSku is not null,
            LineItemType.ParcelShipping => item.Shipping is not null,
            LineItemType.MoneyOrder => item.MoneyOrder is { Amount.Amount: > 0 }
                && !string.IsNullOrWhiteSpace(item.MoneyOrder.Sender)
                && !string.IsNullOrWhiteSpace(item.MoneyOrder.Recipient)
                && item.MoneyOrder.Amount.Currency == item.MoneyOrder.Fee.Currency
                && item.UnitPrice == item.MoneyOrder.Amount + item.MoneyOrder.Fee,
            LineItemType.IdentityVerification => item.IdVerification is { } verification
                && Enum.IsDefined(verification.DocType),
            _ => false
        };
        if (!isValid)
        {
            throw new DomainValidationException($"Line item details do not match item type {item.ItemType}.");
        }

        _lineItems.Add(item);
    }

    public void Complete(string paymentMethod, Money highValueThreshold)
    {
        if (Status != TransactionStatus.Open) throw new DomainValidationException("Transaction is not open for completion.");
        if (LineItems.Count == 0) throw new DomainValidationException("Cannot complete an empty basket.");
        if (paymentMethod is not ("Cash" or "Card" or "EFTPOS")) throw new DomainValidationException("A supported payment method is required.");
        if (highValueThreshold.Amount < 0) throw new DomainValidationException("The high-value money-order threshold cannot be negative.");

        var highValueOrders = LineItems.Where(i => i.ItemType == LineItemType.MoneyOrder && i.MoneyOrder is not null && i.MoneyOrder.Amount > highValueThreshold).ToList();
        if (highValueOrders.Count > 0)
        {
            var hasVerifiedId = LineItems.Any(i => i.ItemType == LineItemType.IdentityVerification && i.IdVerification is { IsVerified: true });
            if (!hasVerifiedId) throw new DomainValidationException($"Money order exceeding {highValueThreshold.Amount} {highValueThreshold.Currency} requires successful identity verification in the same basket.");
        }

        PaymentMethod = paymentMethod;
        Status = TransactionStatus.Paid;
        AddDomainEvent(new TransactionCompletedEvent(Id, BranchCode, GrandTotal));
        foreach (var moneyOrder in LineItems.Where(item => item.MoneyOrder is not null))
        {
            AddDomainEvent(new MoneyOrderIssuedEvent(Id, BranchCode, moneyOrder.MoneyOrder!.Amount, moneyOrder.MoneyOrder.Recipient));
        }
    }

    public void Refund(Staff manager, string reason)
    {
        EnsureManagerForTransaction(manager);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainValidationException("A refund reason is required.");
        if (Status != TransactionStatus.Paid) throw new DomainValidationException("Only paid transactions can be refunded.");

        Status = TransactionStatus.Refunded;
        VoidReason = reason;
        VoidedByManagerId = manager.Id;
        AddDomainEvent(new TransactionVoidedEvent(Id, BranchCode, manager.Id, reason));
    }

    public void MarkReversalInventoryRestocked()
    {
        if (Status is not (TransactionStatus.Refunded or TransactionStatus.Voided))
        {
            throw new DomainValidationException("Only voided or refunded transactions can complete inventory restocking.");
        }

        InventoryRestocked = true;
    }

    public void Void(Staff manager, string reason)
    {
        EnsureManagerForTransaction(manager);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainValidationException("A void reason is required.");
        if (Status != TransactionStatus.Paid) throw new DomainValidationException("Only paid transactions can be voided.");

        Status = TransactionStatus.Voided;
        VoidReason = reason;
        VoidedByManagerId = manager.Id;
        AddDomainEvent(new TransactionVoidedEvent(Id, BranchCode, manager.Id, reason));
    }

    private void EnsureManagerForTransaction(Staff manager)
    {
        if (!manager.IsActive || manager.Role != StaffRole.BranchManager || manager.HomeBranchCode != BranchCode)
        {
            throw new UnauthorizedDomainActionException("An active Branch Manager assigned to this branch is required.");
        }
    }
}

public static class TransactionExtensions
{
    extension(Transaction transaction)
    {
        public Money RetailTotal => transaction.TotalFor(item => item.ItemType == LineItemType.RetailProduct);
        public Money ServicesTotal => transaction.TotalFor(item => item.ItemType != LineItemType.RetailProduct);

        private Money TotalFor(Func<TransactionLineItem, bool> selector) =>
            transaction.LineItems.Where(selector).Aggregate(Money.Zero, (total, item) => total + item.GrandTotal);
    }
}
