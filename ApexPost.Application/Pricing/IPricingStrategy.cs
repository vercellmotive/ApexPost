using ApexPost.Domain.Entities;
using ApexPost.Domain.Common;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Application.Pricing;

public interface IPricingStrategy
{
    Money CalculateShippingPrice(ShippingDetails details);
    Money CalculateShippingPrice(ShippingDetails details, decimal zoneMultiplier);
    void ValidateWeightLimits(ShippingDetails details);
}

public class TieredShippingPricingStrategy : IPricingStrategy
{
    private readonly Func<ShippingDetails, decimal, Money> _pricingRule;

    public TieredShippingPricingStrategy(Func<ShippingDetails, decimal, Money>? pricingRule = null)
    {
        _pricingRule = pricingRule
            ?? ((details, zoneMultiplier) => CalculateDefaultPrice(details, zoneMultiplier));
    }

    public void ValidateWeightLimits(ShippingDetails details)
    {
        var maxWeight = (details.IsParcel, details.ServiceLevel) switch
        {
            (true, ServiceLevel.Standard) => Weight.FromKilograms(20.0m),
            (true, ServiceLevel.Express) => Weight.FromKilograms(10.0m),
            (true, ServiceLevel.Registered) => Weight.FromKilograms(2.0m),
            (false, ServiceLevel.Standard or ServiceLevel.Express or ServiceLevel.Registered) => Weight.FromKilograms(0.5m),
            _ => throw new DomainValidationException("Unsupported shipping service level.")
        };

        var itemType = details.IsParcel ? "Parcel" : "Letter";
        if (details.Weight.Kilograms <= 0) throw new DomainValidationException($"{itemType} weight must be greater than zero.");
        if (details.Weight > maxWeight) throw new DomainValidationException($"{itemType} weight ({details.Weight.Kilograms} kg) exceeds limit of {maxWeight.Kilograms} kg for {details.ServiceLevel}.");
        if (details.IsParcel
            && (details.Dimensions.LengthCm <= 0 || details.Dimensions.WidthCm <= 0 || details.Dimensions.HeightCm <= 0))
        {
            throw new DomainValidationException("Parcel dimensions must be greater than zero.");
        }
    }


    public Money CalculateShippingPrice(ShippingDetails details)
    {
        var zoneMultiplier = details.DestinationZone switch
        {
            "Zone 1" or "Zone 2" => 1.0m,
            "Zone 3" or "Zone 4" => 1.25m,
            "Zone 5" or "Zone 6" => 1.5m,
            "Zone 7" or "Zone 8" => 1.75m,
            "Zone 9" or "Zone 10" => 2.0m,
            _ => throw new DomainValidationException("The destination zone is not supported.")
        };

        return CalculateShippingPrice(details, zoneMultiplier);
    }

    public Money CalculateShippingPrice(ShippingDetails details, decimal zoneMultiplier)
    {
        ValidateWeightLimits(details);
        if (zoneMultiplier <= 0)
        {
            throw new DomainValidationException("The configured shipping zone multiplier must be positive.");
        }

        return _pricingRule(details, zoneMultiplier);
    }

    public static Money CalculateDefaultPrice(
        ShippingDetails details,
        decimal zoneMultiplier,
        decimal parcelWeightSurchargePerKilogram = 1.50m)
    {
        if (zoneMultiplier <= 0)
        {
            throw new DomainValidationException("The configured shipping zone multiplier must be positive.");
        }

        if (parcelWeightSurchargePerKilogram < 0)
        {
            throw new DomainValidationException("The parcel weight surcharge cannot be negative.");
        }

        decimal baseRate = (details.IsParcel, details.ServiceLevel) switch
        {
            (false, ServiceLevel.Standard) => 1.50m,
            (false, ServiceLevel.Express) => 4.50m,
            (false, ServiceLevel.Registered) => 6.00m,
            (true, ServiceLevel.Standard) => 10.00m,
            (true, ServiceLevel.Express) => 18.00m,
            (true, ServiceLevel.Registered) => 24.00m,
            _ => throw new DomainValidationException("Unsupported shipping service level.")
        };

        var weightSurcharge = details.IsParcel
            ? details.Weight.Kilograms * parcelWeightSurchargePerKilogram
            : 0m;
        return new Money((baseRate * zoneMultiplier) + weightSurcharge);
    }

}