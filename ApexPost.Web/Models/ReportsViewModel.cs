using ApexPost.Application.Queries;

namespace ApexPost.Web.Models;

public sealed record ReportsViewModel(
    DateOnly ReportDate,
    int LowStockThreshold,
    IReadOnlyList<DailySalesRowDto> Sales,
    IReadOnlyList<LowStockRowDto> LowStock,
    IReadOnlyList<ShippingVolumeRowDto> Shipping);
