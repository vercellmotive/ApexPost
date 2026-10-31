using System.ComponentModel.DataAnnotations;
using ApexPost.Application.Queries;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ApexPost.Web.Models;

public class TransactionFormViewModel
{
    [BindNever]
    [ValidateNever]
    public CheckoutOptionsDto? Options { get; set; }

    [Required]
    public string CounterId { get; set; } = string.Empty;

    public List<RetailItemInputViewModel> RetailItems { get; set; } =
        Enumerable.Range(0, 5).Select(_ => new RetailItemInputViewModel()).ToList();

    public List<ShipmentInputViewModel> Shipments { get; set; } =
        Enumerable.Range(0, 3).Select(_ => new ShipmentInputViewModel()).ToList();

    public List<MoneyOrderInputViewModel> MoneyOrders { get; set; } =
        Enumerable.Range(0, 3).Select(_ => new MoneyOrderInputViewModel()).ToList();

    public bool IncludeIdentityVerification { get; set; }
    [EnumDataType(typeof(DocumentType))]
    public DocumentType DocumentType { get; set; }
    public string? DocumentNumber { get; set; }

    [Required]
    [RegularExpression("Cash|Card|EFTPOS")]
    public string PaymentMethod { get; set; } = "Cash";
}

public sealed class RetailItemInputViewModel
{
    [StringLength(50)]
    public string? ProductSku { get; set; }

    [Range(1, 100)]
    public int? Quantity { get; set; }
}

public sealed class ShipmentInputViewModel
{
    public bool Include { get; set; }
    public bool IsParcel { get; set; } = true;

    [Range(typeof(decimal), "0.01", "20000")]
    public decimal WeightKg { get; set; } = 1;

    [Range(typeof(decimal), "0", "300")]
    public decimal LengthCm { get; set; } = 30;

    [Range(typeof(decimal), "0", "300")]
    public decimal WidthCm { get; set; } = 20;

    [Range(typeof(decimal), "0", "300")]
    public decimal HeightCm { get; set; } = 10;

    [RegularExpression("^Zone (?:[1-9]|10)$")]
    public string DestinationZone { get; set; } = "Zone 1";

    [EnumDataType(typeof(ServiceLevel))]
    public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Standard;

    [StringLength(120)]
    public string? Street { get; set; }

    [StringLength(60)]
    public string? City { get; set; }

    [StringLength(30)]
    public string? State { get; set; }

    [StringLength(12)]
    public string? PostalCode { get; set; }

    public Address? ToDestination() => string.IsNullOrWhiteSpace(Street)
        ? null
        : new Address(Street.Trim(), City?.Trim() ?? string.Empty, State?.Trim() ?? string.Empty, PostalCode?.Trim() ?? string.Empty, "Australia");
}

public sealed class MoneyOrderInputViewModel
{
    [Range(typeof(decimal), "0.01", "20000")]
    public decimal? Amount { get; set; }

    [StringLength(100)]
    public string? Sender { get; set; }

    [StringLength(100)]
    public string? Recipient { get; set; }
}
