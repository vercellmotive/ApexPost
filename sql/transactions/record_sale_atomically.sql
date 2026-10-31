-- Records a sale, its line, its payment and the stock decrement as one atomic unit.
-- Tables are always touched in the same order: BranchStock, CounterTransactions, TransactionLines, Payments.
-- This prevents the lock-order deadlock of one session taking stock first while another takes the sale first.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TransactionId uniqueidentifier = NEWID();
DECLARE @LineId uniqueidentifier = NEWID();
DECLARE @BranchCode varchar(10) = 'B001';
DECLARE @Counter varchar(10) = 'C01';
DECLARE @Clerk varchar(20) = 'STF002';
DECLARE @Sku varchar(20) = 'SKU-001';
DECLARE @Quantity int = 1;
DECLARE @UnitPrice decimal(18,2);
DECLARE @TaxCategory varchar(20);
DECLARE @ProductName nvarchar(100);
DECLARE @Tax decimal(18,2);

BEGIN TRY
    SELECT @UnitPrice = UnitPrice, @TaxCategory = TaxCategory, @ProductName = Name
    FROM dbo.Products
    WHERE Sku = @Sku;

    IF @UnitPrice IS NULL
        THROW 50404, N'The product was not found.', 1;

    SET @Tax = ROUND(@UnitPrice * @Quantity * CASE @TaxCategory WHEN 'StandardGst' THEN 0.10 WHEN 'Reduced' THEN 0.05 ELSE 0 END, 2);

    BEGIN TRANSACTION;

    -- The WHERE clause makes the check and the decrement a single atomic statement.
    UPDATE dbo.BranchStock
    SET QuantityOnHand = QuantityOnHand - @Quantity,
        ModifiedAtUtc = SYSUTCDATETIME()
    WHERE BranchCode = @BranchCode
      AND Sku = @Sku
      AND QuantityOnHand >= @Quantity;

    IF @@ROWCOUNT = 0
        THROW 50006, N'Insufficient stock: the sale was not recorded.', 1;

    INSERT dbo.CounterTransactions (Id, BranchCode, CounterCode, ClerkStaffId, Status, BusinessDate, InventoryRestocked)
    VALUES (@TransactionId, @BranchCode, @Counter, @Clerk, 'Paid', CAST(SYSUTCDATETIME() AS date), 0);

    INSERT dbo.TransactionLines (Id, TransactionId, LineNumber, Description, LineType, Quantity, UnitPrice, TaxCategory, TaxAmount, LineTotal, Sku)
    VALUES (@LineId, @TransactionId, 1, @ProductName, 'RetailProduct', @Quantity, @UnitPrice, @TaxCategory, @Tax, (@UnitPrice * @Quantity) + @Tax, @Sku);

    INSERT dbo.Payments (Id, TransactionId, Kind, Method, Amount)
    VALUES (NEWID(), @TransactionId, 'Sale', 'Card', (@UnitPrice * @Quantity) + @Tax);

    COMMIT TRANSACTION;
    SELECT @TransactionId AS TransactionId;
END TRY
BEGIN CATCH
    -- XACT_STATE: 1 is committable, -1 is doomed and can only roll back, 0 means no transaction is open.
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
