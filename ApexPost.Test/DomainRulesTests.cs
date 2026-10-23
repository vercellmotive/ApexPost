using ApexPost.Application.Pricing;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Test
{
    public class DomainRulesTests
    {
        private readonly TieredShippingPricingStrategy _pricing = new();

        [Fact]
        public void Rule1_MoneyOrder_OverThreshold_WithoutId_Throws()
        {
            var tx = new Transaction
            {
                Id = Guid.NewGuid(),
                BranchCode = new Domain.ValueObjects.BranchCode("B001"),
                CounterId = "C01",
                ClerkStaffId = "STF01"
            };

            tx.AddLineItem(new TransactionLineItem(Guid.NewGuid(), "Money Order", Domain.Enums.LineItemType.MoneyOrder, 1, new Domain.ValueObjects.Money(1500m), Domain.Enums.TaxCategory.Exempt,
                MoneyOrder: new MoneyOrderDetails(new Domain.ValueObjects.Money(1500m), "Alice", "Bob", new Domain.ValueObjects.Money(10m))));

            Assert.Throws<DomainValidationException>(() => tx.Complete("Cash", new Domain.ValueObjects.Money(1000m)));
        }

        [Fact]
        public void Rule2_MoneyOrder_OverThreshold_WithId_Succeeds()
        {
            var tx = new Transaction
            {
                Id = Guid.NewGuid(),
                BranchCode = new Domain.ValueObjects.BranchCode("B001"),
                CounterId = "C01",
                ClerkStaffId = "STF01"
            };

            tx.AddLineItem(new TransactionLineItem(Guid.NewGuid(), "Money Order", Domain.Enums.LineItemType.MoneyOrder, 1, new Domain.ValueObjects.Money(1500m), Domain.Enums.TaxCategory.Exempt, MoneyOrder: new MoneyOrderDetails(new Domain.ValueObjects.Money(1500m), "Alice", "Bob", new Domain.ValueObjects.Money(10m))));

            tx.AddLineItem(new TransactionLineItem(Guid.NewGuid(), "ID Check", Domain.Enums.LineItemType.IdentityVerification, 1, Money.Zero, Domain.Enums.TaxCategory.Exempt, IdVerification: new IdVerificationDetails(Domain.Enums.DocumentType.Passport, "******1234", true)));

            tx.Complete("Cash", new Money(1000m));
            Assert.Equal(TransactionStatus.Paid, tx.Status);
        }

        [Fact]
        public void Rule3_Parcel_OverWeightLimit_Throws()
        {
            var parcel = new ShippingDetails(true, Weight.FromKilograms(13.5m), new Dimensions(10, 10, 10), "Zone 1", ServiceLevel.Express);

            Assert.Throws<ArgumentException>(() => _pricing.ValidateWeightLimits(parcel));
        }

        [Fact]
        public void Rule4_NonManager_Void_ThrowsUnauthorized()
        {
            var tx = new Transaction
            {
                Id = Guid.NewGuid(),
                BranchCode = new BranchCode("B001"),
                CounterId = "C01",
                ClerkStaffId = "STF01"
            };
            tx.AddLineItem(new TransactionLineItem(Guid.NewGuid(), "Stamp", LineItemType.RetailProduct, 1, new Money(2m), TaxCategory.StandardGst));
            tx.Complete("Cash", new Money(1000m));

            var clerk = new Staff
            {
                Id = "STF01",
                Name = "John",
                Email = "j@apex.com",
                Role = StaffRole.CounterClerk,
                HomeBranchCode = new BranchCode("B001")
            };

            Assert.Throws<UnauthorizedDomainActionException>(() => tx.Void(clerk, "Customer changed mind"));
        }
    }
}
