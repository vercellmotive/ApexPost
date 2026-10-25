using System;
using System.Collections.Generic;
using System.Text;

namespace ApexPost.Domain.Enums
{
    public enum StaffRole
    {
        CounterClerk,
        BranchManager,
        Auditor,
        Administrator
    }

    public enum ServiceLevel
    {
        Standard,
        Express,
        Registered
    }

    public enum TaxCategory
    {
        Exempt,
        StandardGst,
        Reduced
    }

    public enum TransactionStatus
    {
        Open,
        Paid,
        Voided,
        Refunded
    }

    public enum LineItemType
    {
        RetailProduct, 
        ParcelShipping,
        MoneyOrder,
        IdentityVerification
    }

    public enum DocumentType
    {
        Passport,
        DriversLicense,
        NationalIdentityCard
    }

    public enum StockAdjustmentReason
    {
        Replenishment,
        Damage,
        CountCorrection,
        CustomerReturn
    }
}
