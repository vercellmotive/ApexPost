using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Web.Models;

public sealed class ShippingLabelViewModel
{
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

    [Required]
    [StringLength(100)]
    public string RecipientName { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Street { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string State { get; set; } = string.Empty;

    [Required]
    [StringLength(12)]
    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = "Australia";

    public Address DestinationAddress => new(Street, City, State, PostalCode, Country);

    public decimal? Price { get; set; }
}
