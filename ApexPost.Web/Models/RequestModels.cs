using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class TransactionReversalRequest
{
    [Required]
    public Guid? TransactionId { get; set; }

    [Required]
    [StringLength(200)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class StockAdjustmentRequest
{
    [Required]
    [RegularExpression("^[A-Za-z0-9_-]{1,20}$")]
    public string ProductSku { get; set; } = string.Empty;

    public int QuantityDelta { get; set; }

    [EnumDataType(typeof(StockAdjustmentReason))]
    public StockAdjustmentReason ReasonCode { get; set; }
}

public sealed class BranchStatusRequest
{
    public bool IsActive { get; set; }
}
