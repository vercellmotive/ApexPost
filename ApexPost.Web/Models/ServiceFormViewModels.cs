using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class MoneyOrderFormViewModel
{
    [Required]
    public string CounterId { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "20000")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(100)]
    public string Sender { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Recipient { get; set; } = string.Empty;

    public bool IncludeIdentityVerification { get; set; }

    [EnumDataType(typeof(DocumentType))]
    public DocumentType DocumentType { get; set; }

    [StringLength(64)]
    public string? DocumentNumber { get; set; }

    [Required]
    [RegularExpression("Cash|Card|EFTPOS")]
    public string PaymentMethod { get; set; } = "Cash";
}

public sealed class IdentityVerificationFormViewModel
{
    [Required]
    public string CounterId { get; set; } = string.Empty;

    [EnumDataType(typeof(DocumentType))]
    public DocumentType DocumentType { get; set; }

    [Required]
    [StringLength(64)]
    public string DocumentNumber { get; set; } = string.Empty;
}
