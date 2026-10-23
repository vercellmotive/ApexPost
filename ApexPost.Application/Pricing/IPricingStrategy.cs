using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Application.Pricing;

public interface IPricingStrategy
{
    Money CalculateShippingPrice(ShippingDetails details);
    void ValidateWeightLimits(ShippingDetails details);
}

public class TieredShippingPricingStrategy : IPricingStrategy
{
    public void ValidateWeightLimits(ShippingDetails details)
    {
        var maxWeight = details.ServiceLevel switch
        {
            ServiceLevel.Standard => Weight.FromKilograms(20.0m),
            ServiceLevel.Express => Weight.FromKilograms(10.0m),
            ServiceLevel.Registered => Weight.FromKilograms(2.0m),
            _ => throw new ArgumentOutOfRangeException(nameof(details.ServiceLevel))
        };

        if (details.Weight > maxWeight) throw new ArgumentException($"Parcel weight ({details.Weight.Kilograms} kg) exceeds limit of {maxWeight.Kilograms} kg for {details.ServiceLevel}.");
    }


    public Money CalculateShippingPrice(ShippingDetails details)
    {
        ValidateWeightLimits(details);

        decimal baseRate = (details.IsParcel, details.ServiceLevel) switch
        {
            (false, ServiceLevel.Standard) => 1.50m,
            (false, ServiceLevel.Express) => 4.50m,
            (false, ServiceLevel.Registered) => 6.00m,
            (true, ServiceLevel.Standard) => 10.00m,
            (true, ServiceLevel.Express) => 18.00m,
            (true, ServiceLevel.Registered) => 24.00m,
            _ => 10.00m
        };

        decimal zoneMultiplier = details.DestinationZone switch
        {
            "Zone 1" or "Zone 2" => 1.0m,
            "Zone 3" or "Zone 4" => 1.25m,
            "Zone 5" or "Zone 6" => 1.5m,
            _ => 2.0m
        };

        decimal weightSurcharge = details.IsParcel ? details.Weight.Kilograms * 1.50m : 0m;
        return new Money((baseRate * zoneMultiplier) + weightSurcharge);
    }

}