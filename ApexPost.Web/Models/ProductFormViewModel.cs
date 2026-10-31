using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class PriceRevisionViewModel
{
    [Required]
    public string Category { get; set; } = string.Empty;

    [Range(typeof(decimal), "-99", "100")]
    public decimal PercentChange { get; set; }
}

public sealed class ProductFormViewModel
{
    public bool IsExisting { get; set; }

    [Required]
    [StringLength(20)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "100000")]
    public decimal UnitPrice { get; set; }

    [EnumDataType(typeof(TaxCategory))]
    public TaxCategory TaxCategory { get; set; } = TaxCategory.StandardGst;
}
