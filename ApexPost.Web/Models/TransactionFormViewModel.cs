using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public class TransactionFormViewModel
{
    [Required]
    public string CounterId { get; set; } = string.Empty;

    public List<RetailItemInputViewModel> RetailItems { get; set; } =
        Enumerable.Range(0, 5).Select(_ => new RetailItemInputViewModel()).ToList();

    public bool IncludeShipping { get; set; }
    public bool IsParcel { get; set; } = true;

    [Range(typeof(decimal), "0.01", "20000")]
    public decimal ShippingWeightKg { get; set; } = 1;

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

    public decimal? MoneyOrderAmount { get; set; }
    public string? Sender { get; set; }
    public string? Recipient { get; set; }
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
