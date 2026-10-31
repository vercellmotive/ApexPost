using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Infrastructure.Persistence;

// Explicit mapping between domain aggregates and persistence records; nothing here touches the database.
public static class PersistenceMapper
{
    public static Branch ToDomain(BranchRecord record) => new()
    {
        Id = record.Code,
        Name = record.Name,
        Region = record.Region,
        OpenTime = record.OpenTime,
        CloseTime = record.CloseTime,
        TimeZoneId = record.TimeZoneId,
        IsActive = record.IsActive,
        CounterTerminals = record.Counters.Select(counter => counter.TerminalCode).ToList()
    };

    public static BranchRecord ToRecord(Branch branch)
    {
        var record = new BranchRecord { Code = branch.Id };
        CopyTo(branch, record);
        return record;
    }

    public static void CopyTo(Branch branch, BranchRecord record)
    {
        record.Name = branch.Name;
        record.Region = branch.Region;
        record.OpenTime = branch.OpenTime;
        record.CloseTime = branch.CloseTime;
        record.TimeZoneId = branch.TimeZoneId;
        record.IsActive = branch.IsActive;
        record.Counters.RemoveAll(counter => !branch.CounterTerminals.Contains(counter.TerminalCode, StringComparer.Ordinal));
        foreach (var terminal in branch.CounterTerminals.Where(terminal => record.Counters.All(counter => counter.TerminalCode != terminal)))
        {
            record.Counters.Add(new CounterRecord { BranchCode = branch.Id, TerminalCode = terminal });
        }
    }

    public static Product ToDomain(ProductRecord record) => new()
    {
        Id = record.Sku,
        Name = record.Name,
        Category = record.Category?.Name ?? throw new InvalidOperationException($"Product {record.Sku.Value} was loaded without its category."),
        UnitPrice = new Money(record.UnitPrice),
        TaxCategory = record.TaxCategory
    };

    public static ProductRecord ToRecord(Product product)
    {
        var record = new ProductRecord { Sku = product.Id };
        CopyTo(product, record);
        return record;
    }

    public static void CopyTo(Product product, ProductRecord record)
    {
        record.Name = product.Name;
        record.CategoryId = ReferenceData.CategoryIds[product.Category];
        record.UnitPrice = product.UnitPrice.Amount;
        record.TaxCategory = product.TaxCategory;
    }

    public static CounterTransactionRecord ToRecord(Transaction transaction)
    {
        var record = new CounterTransactionRecord
        {
            Id = transaction.Id,
            BranchCode = transaction.BranchCode,
            CounterCode = transaction.CounterId,
            ClerkStaffId = transaction.ClerkStaffId,
            OccurredAtUtc = transaction.CreatedAt.UtcDateTime,
            BusinessDate = transaction.BusinessDate
        };
        record.Lines = transaction.LineItems.Select(ToRecord).ToList();
        foreach (var (line, number) in record.Lines.Select((line, index) => (line, index + 1)))
        {
            line.LineNumber = number;
        }

        if (transaction.Status != TransactionStatus.Open)
        {
            // Every completed sale has its sale payment, including history that is already reversed.
            record.Payments.Add(CreatePayment(transaction, PaymentKinds.Sale, transaction.PaymentMethod));
        }

        CopyStateTo(transaction, record);
        return record;
    }

    // Only state that can change after a sale is copied, so Status and audit data cannot arrive any other way.
    public static void CopyStateTo(Transaction transaction, CounterTransactionRecord record)
    {
        record.Status = transaction.Status;
        record.VoidReason = transaction.VoidReason;
        record.VoidedByStaffId = transaction.VoidedByManagerId;
        record.InventoryRestocked = transaction.InventoryRestocked;

        var kind = transaction.Status switch
        {
            TransactionStatus.Paid => PaymentKinds.Sale,
            TransactionStatus.Voided => PaymentKinds.Void,
            TransactionStatus.Refunded => PaymentKinds.Refund,
            _ => null
        };
        if (kind is not null && record.Payments.All(payment => payment.Kind != kind))
        {
            record.Payments.Add(CreatePayment(transaction, kind, record.Payments.FirstOrDefault()?.Method ?? transaction.PaymentMethod));
        }
    }

    private static PaymentRecord CreatePayment(Transaction transaction, string kind, string method) => new()
    {
        Id = Guid.NewGuid(),
        TransactionId = transaction.Id,
        Kind = kind,
        Method = method,
        Amount = transaction.GrandTotal,
        PaidAtUtc = DateTime.UtcNow
    };

    public static Transaction ToDomain(CounterTransactionRecord record) =>
        Transaction.Rehydrate(
            record.Id,
            record.BranchCode,
            record.CounterCode,
            record.ClerkStaffId,
            record.Status,
            record.Lines.OrderBy(line => line.LineNumber).Select(ToDomain),
            record.Payments.FirstOrDefault(payment => payment.Kind == PaymentKinds.Sale)?.Method ?? "Pending",
            new DateTimeOffset(DateTime.SpecifyKind(record.OccurredAtUtc, DateTimeKind.Utc)),
            record.BusinessDate,
            record.VoidReason,
            record.VoidedByStaffId,
            record.InventoryRestocked);

    public static TransactionLineRecord ToRecord(TransactionLineItem item)
    {
        var record = new TransactionLineRecord
        {
            Id = item.Id,
            Description = item.Description,
            LineType = item.ItemType,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TaxCategory = item.TaxCategory,
            TaxAmount = item.TaxAmount,
            LineTotal = item.GrandTotal.Amount,
            Sku = item.ProductSku
        };

        if (item.Shipping is { } shipping)
        {
            record.Shipment = new ShipmentRecord
            {
                Id = Guid.NewGuid(),
                TransactionLineId = item.Id,
                ShippingRateId = ReferenceData.ShippingRateId(shipping.DestinationZone, shipping.IsParcel, shipping.ServiceLevel),
                Weight = shipping.Weight,
                Dimensions = new ParcelDimensions
                {
                    LengthCm = shipping.Dimensions.LengthCm,
                    WidthCm = shipping.Dimensions.WidthCm,
                    HeightCm = shipping.Dimensions.HeightCm
                },
                Destination = shipping.Destination is { } address
                    ? new ShipmentAddress { Street = address.Street, City = address.City, State = address.State, PostalCode = address.PostalCode, Country = address.Country }
                    : null
            };
        }

        if (item.MoneyOrder is { } moneyOrder)
        {
            record.MoneyOrder = new MoneyOrderRecord
            {
                Id = Guid.NewGuid(),
                TransactionLineId = item.Id,
                Amount = moneyOrder.Amount,
                Fee = moneyOrder.Fee,
                Sender = moneyOrder.Sender,
                Recipient = moneyOrder.Recipient
            };
        }

        if (item.IdVerification is { } verification)
        {
            record.IdentityVerification = ToRecord(verification, item.Id);
        }

        return record;
    }

    // The domain type only ever holds a masked value, so a full document number cannot reach this record.
    public static IdentityVerificationRecord ToRecord(IdVerificationDetails verification, Guid lineId) => new()
    {
        Id = Guid.NewGuid(),
        TransactionLineId = lineId,
        DocumentType = verification.DocType,
        MaskedDocumentNumber = verification.MaskedDocNumber,
        IsVerified = verification.IsVerified
    };

    public static IdVerificationDetails ToDomain(IdentityVerificationRecord record) =>
        new(record.DocumentType, record.MaskedDocumentNumber, record.IsVerified);

    private static TransactionLineItem ToDomain(TransactionLineRecord record)
    {
        ShippingDetails? shipping = null;
        if (record.Shipment is { } shipment)
        {
            var rate = shipment.Rate ?? throw new InvalidOperationException("A shipment was loaded without its rate.");
            shipping = new ShippingDetails(
                rate.IsParcel,
                shipment.Weight,
                new Dimensions(shipment.Dimensions.LengthCm, shipment.Dimensions.WidthCm, shipment.Dimensions.HeightCm),
                rate.ZoneId,
                rate.ServiceLevel,
                shipment.Destination is { Street: not null } address
                    ? new Address(address.Street, address.City ?? string.Empty, address.State ?? string.Empty, address.PostalCode ?? string.Empty, address.Country ?? string.Empty)
                    : null);
        }

        return new TransactionLineItem(
            record.Id,
            record.Description,
            record.LineType,
            record.Quantity,
            record.UnitPrice,
            record.TaxCategory,
            record.Sku,
            shipping,
            record.MoneyOrder is { } order ? new MoneyOrderDetails(order.Amount, order.Sender, order.Recipient, order.Fee) : null,
            record.IdentityVerification is { } verification ? ToDomain(verification) : null);
    }
}
