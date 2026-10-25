using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Enums;

namespace ApexPost.Web.Models;

public sealed class StaffFormViewModel
{
    public string? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [EnumDataType(typeof(StaffRole))]
    public StaffRole Role { get; set; }

    [Required]
    public string HomeBranchCode { get; set; } = string.Empty;
}
