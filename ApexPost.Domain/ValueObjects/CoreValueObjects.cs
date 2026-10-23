using ApexPost.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Domain.ValueObjects
{
    public readonly record struct Money
    {
        public decimal Amount
        {
            get;
            init => field = value >= 0 ? Math.Round(value, 2, MidpointRounding.AwayFromZero) : throw new ArgumentOutOfRangeException(nameof(Amount), "Amount cannot be negative.");
        }

        public string Currency { get; init; } = "AUD";

        public Money(decimal amount, string currency = "AUD")
        {
            Amount = amount;
            Currency = currency;
        }

        public static Money Zero => new(0m);
        public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount, left.Currency);
        public static Money operator -(Money left, Money right) => new(left.Amount - right.Amount, left.Currency);
        public static Money operator *(Money money, decimal multiplier) => new(money.Amount * multiplier, money.Currency);

        public static bool operator >(Money left, Money right) => (left.Amount > right.Amount);
        public static bool operator <(Money left, Money right) => (left.Amount < right.Amount);
        public static bool operator >=(Money left, Money right) => (left.Amount >= right.Amount);
        public static bool operator <=(Money left, Money right) => (left.Amount <= right.Amount);
    }

    public static class MoneyExtensions
    {
        public static Money CalculateTax(this Money money, TaxCategory category) => category switch
        {
            TaxCategory.Exempt => Money.Zero,
            TaxCategory.StandardGst => money * 0.10m,
            TaxCategory.Reduced => money * 0.05m,
            _ => throw new ArgumentOutOfRangeException(nameof(category), "Invalid tax category.")
        };
    }

    public readonly record struct Weight
    {
        public decimal Kilograms { get; init; }

        private Weight(decimal kilograms)
        {
            if(kilograms < 0) throw new ArgumentOutOfRangeException(nameof(Kilograms), "Weight cannot be negative.");
            Kilograms = kilograms;
        }

        public static Weight FromKilograms(decimal kg) => new(kg);
        public static Weight FromGrams(decimal grams) => new(grams / 1000m);

        public static bool operator >(Weight left, Weight right) => left.Kilograms > right.Kilograms;
        public static bool operator <(Weight left, Weight right) => left.Kilograms < right.Kilograms;
    }

    public readonly record struct Dimensions(decimal LengthCm, decimal WidthCm, decimal HeightCm);
    public readonly record struct Sku(string Value);
    public readonly record struct BranchCode(string Value);
    public readonly record struct Address(string Street, string City, string State, string PostalCode, string Country);
}
