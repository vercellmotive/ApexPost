using ApexPost.Domain.Common;
using ApexPost.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Domain.Events;

public record TransactionCompletedEvent(Guid TransactionId, BranchCode BranchCode, Money TotalAmount) : DomainEvent;
public record StockLowEvent(BranchCode BranchCode, Sku Sku, int RemainingQuantity) : DomainEvent;
public record MoneyOrderIssuedEvent(Guid TransactionId, BranchCode BranchCode, Money Amount, string Recipient) : DomainEvent;
public record TransactionVoidedEvent(Guid TransactionId, BranchCode BranchCode, string ManagerStaffId, string Reason) : DomainEvent;
