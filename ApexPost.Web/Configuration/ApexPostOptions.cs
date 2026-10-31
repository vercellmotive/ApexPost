using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Configuration;

public sealed class SecurityOptions
{
    public const string Section = "Security";
    public string[] AllowedIps { get; set; } = [];
}

public sealed class MaintenanceOptions
{
    public const string Section = "MaintenanceMode";
    public bool Enabled { get; set; }

    [Range(1, 86400)]
    public int RetryAfterSeconds { get; set; } = 120;
}

public sealed class PerformanceOptions
{
    public const string Section = "Performance";

    [Range(1, int.MaxValue)]
    public int SlowActionThresholdMs { get; set; } = 500;

    [Range(1, int.MaxValue)]
    public int SlowRequestThresholdMs { get; set; } = 500;
}

public sealed class IntegrationOptions
{
    public const string Section = "Integration";
    public string? ApiKey { get; set; }
    public Dictionary<string, PartnerCredentialOptions> Partners { get; set; } = [];
}

public sealed class PartnerCredentialOptions
{
    public string? ApiKey { get; set; }
}

public sealed class PartnerCorsOptions
{
    public const string Section = "Cors";

    [Required]
    [Url]
    public string PartnerTrackingWidgetOrigin { get; set; } = "https://tracking-widget.apexpost.example";
}

public sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";
    public FixedWindowPolicyOptions Login { get; set; } = new() { PermitLimit = 3, WindowSeconds = 60 };
    public FixedWindowPolicyOptions Partner { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
}

public sealed class FixedWindowPolicyOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; }

    [Range(1, 86400)]
    public int WindowSeconds { get; set; }
}

public sealed class TransactionOptions
{
    public const string Section = "Transactions";

    [Range(typeof(decimal), "0", "1000000")]
    public decimal HighValueMoneyOrderThreshold { get; set; } = 1000m;

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal MaximumMoneyOrderAmount { get; set; } = 20000m;

    [Range(typeof(decimal), "0", "1000")]
    public decimal MoneyOrderFee { get; set; } = 10m;
}

public sealed class InventoryOptions
{
    public const string Section = "Inventory";

    [Range(0, int.MaxValue)]
    public int LowStockThreshold { get; set; } = 5;
}

public sealed class ShippingPricingOptions
{
    public const string Section = "ShippingPricing";

    [Range(typeof(decimal), "0", "1000")]
    public decimal ParcelWeightSurchargePerKilogram { get; set; } = 1.50m;
}

public sealed class CourierTrackingOptions
{
    public const string Section = "CourierTracking";

    [Range(1, int.MaxValue)]
    public int TimeoutMs { get; set; } = 1500;

    [Range(0, int.MaxValue)]
    public int SimulatedDelayMs { get; set; } = 50;
}

public sealed class CourierRatesOptions
{
    public const string Section = "CourierRates";

    [Range(1, 256)]
    public int MaxConcurrentRequests { get; set; } = 4;

    [Range(0, int.MaxValue)]
    public int SimulatedDelayMs { get; set; } = 20;
}
