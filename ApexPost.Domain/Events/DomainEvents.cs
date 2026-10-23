using ApexPost.Domain.Common;
using ApexPost.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Domain.Events;

public record TransactionCompletedEvent(Guid TransactionId, BranchCode BranchCode, Money TotalAmount) : IDomainEvent;
public record StockLowEvent(BranchCode BranchCode, Sku Sku, int RemainingQuality): IDomainEvent;
public record MoneyOrderIssuedEvent(Guid TransactionId, BranchCode BranchCode, Money Amount, string Recipient) : IDomainEvent;
public record TransactionVoidedEvent(Guid TransactionId, BranchCode BranchCode, string ManagerStaffId, string Reason): IDomainEvent;
