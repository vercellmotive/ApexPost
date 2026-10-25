using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class ShippingLabelViewModel
{
    public bool IsParcel { get; set; } = true;

    [Range(typeof(decimal), "0.01", "20000")]
    public decimal WeightKg { get; set; } = 1;

    [Range(typeof(decimal), "0.1", "300")]
    public decimal LengthCm { get; set; } = 30;

    [Range(typeof(decimal), "0.1", "300")]
    public decimal WidthCm { get; set; } = 20;

    [Range(typeof(decimal), "0.1", "300")]
    public decimal HeightCm { get; set; } = 10;

    [RegularExpression("^Zone (?:[1-9]|10)$")]
    public string DestinationZone { get; set; } = "Zone 1";

    [EnumDataType(typeof(ServiceLevel))]
    public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Standard;
    public decimal? Price { get; set; }
}
