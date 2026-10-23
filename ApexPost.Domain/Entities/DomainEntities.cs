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
    public bool IsActive { get; set; } = true;
    public List<string> CounterTerminals { get; init; } = [];

    public bool IsOpen(DateTimeOffset currentTime)
    {
        if (IsActive) return false;
        var time = currentTime.TimeOfDay;
        return time >= OpenTime && time <= CloseTime;
    }

    public void EnsureAcceptsTransaction(DateTimeOffset currentTime)
    {
        if (!IsOpen(currentTime))
        {
            throw new DomainValidationException($"Branch {Id.Value} is closed or inactive. Transaction are forbidden");
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

    public void EnsureCanTransact(BranchCode currentBranch)
    {
        if(!IsActive) throw new UnauthorizedDomainActionException($"Staff {Id} is deactivated.");
        if(HomeBranchCode != currentBranch) throw new UnauthorizedDomainActionException($"Staff member {Id} belongs to branch {HomeBranchCode.Value}, not {currentBranch.Value}");
    }
}


public class Production : Entity<Sku>
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required Money UnitPrice { get; init; }
    public required TaxCategory TaxCategory { get; init; }
}

public record ShippingDetails(bool IsParcel, Weight Weight, Dimensions Dimensions, string DestinationZone, ServiceLevel ServiceLevel);
public record MoneyOrderDetails(Money Amount, string Sender, string Recipient, Money Fee);
public record IdVerificationDetails(DocumentType DocType, string MaskedDocNumber, bool IsVerified);

public record TransactionLineItem(
    Guid Id, 
    string Description,
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
    public required BranchCode BranchCode { get; init; }
    public required string CounterId { get; init; }
    public required string ClerkStaffId { get; init; }
    public TransactionStatus Status { get; private set; } = TransactionStatus.Open;
    public List<TransactionLineItem> LineItems { get; init; } = [];
    public string PaymentMethod { get; private set; } = "Pending";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? VoidReason { get; private set; }
    public string? VoidedByManagerId { get; private set; }


    public Money SubTotal => LineItems.Aggregate(Money.Zero, (acc, item) => acc + item.TotalPrice);
    public Money TotalTax => LineItems.Aggregate(Money.Zero, (acc, item) => acc + item.TaxAmount);
    public Money GrandTotal => SubTotal + TotalTax;

    public void AddLineItem(TransactionLineItem item)
    {
        if (Status != TransactionStatus.Open) throw new DomainValidationException($"Cannot modify a finalized or voided transaction.");
        LineItems.Add(item);
    }

    public void Complete(string paymentMethod, Money highValueThreshold)
    {
        if (Status != TransactionStatus.Open) throw new DomainValidationException("Transaction is not open for completion.");
        if (LineItems.Count == 0) throw new DomainValidationException("Cannot complete an empty basket.");


        var highValueOrders = LineItems.Where(i => i.ItemType == LineItemType.MoneyOrder && i.MoneyOrder is not null && i.MoneyOrder.Amount >= highValueThreshold).ToList();
        if (highValueOrders.Count > 0)
        {
            var hasVeridiedId = LineItems.Any(i => i.ItemType == LineItemType.IdentityVerification && i.IdVerification is { IsVerified: true });
            if (!hasVeridiedId) throw new DomainValidationException($"Money order exceeding {highValueThreshold.Amount} {highValueThreshold.Currency} require successful identity verification in the same basket.");

        }

        PaymentMethod = paymentMethod;
        Status = TransactionStatus.Paid;
        AddDomainEvent(new TransactionCompletedEvent(Id, BranchCode, GrandTotal));
    }

    public void Void(Staff manager, string reason)
    {
        if (manager.Role != StaffRole.BranchManager) throw new UnauthorizedDomainActionException("Only a Branch Manager can void transactions.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainValidationException("A void reason is required.");
        if (Status != TransactionStatus.Paid) throw new DomainValidationException("Only paid transactions can be voided.");

        Status = TransactionStatus.Voided;
        VoidReason = reason;
        VoidedByManagerId = manager.Id;
        AddDomainEvent(new TransactionVoidedEvent(Id, BranchCode, manager.Id, reason));
    }
}