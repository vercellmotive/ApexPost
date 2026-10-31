using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;

public sealed class BranchFormViewModel
{
    public bool IsExisting { get; set; }

    [Required]
    [RegularExpression("^[A-Za-z][0-9]{3}$", ErrorMessage = "Branch codes must look like B006.")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Region { get; set; } = string.Empty;

    public TimeSpan OpenTime { get; set; } = new(9, 0, 0);
    public TimeSpan CloseTime { get; set; } = new(17, 0, 0);

    [Required]
    public string TimeZoneId { get; set; } = "Australia/Sydney";

    [Required]
    [Display(Name = "Counter terminals (comma separated)")]
    public string CounterTerminals { get; set; } = string.Empty;
}
