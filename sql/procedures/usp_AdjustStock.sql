-- Changes a stock level and writes its history row as one atomic unit, then returns the new quantity.
CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
    @BranchCode varchar(10),
    @Sku varchar(20),
    @Delta int,
    @Reason varchar(30),
    @ChangedBy varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @BranchCode IS NULL OR @Sku IS NULL OR @ChangedBy IS NULL
        THROW 50003, N'@BranchCode, @Sku and @ChangedBy are required.', 1;
    IF @Delta IS NULL OR @Delta = 0
        THROW 50004, N'@Delta must be a non-zero quantity.', 1;
    IF @Reason IS NULL OR @Reason NOT IN ('Replenishment', 'Damage', 'CountCorrection', 'CustomerReturn')
        THROW 50005, N'@Reason is not a recognised adjustment reason.', 1;

    DECLARE @Updated TABLE (QuantityOnHand int NOT NULL);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Same access order as a sale: BranchStock first, then history, which keeps lock order consistent.
        UPDATE dbo.BranchStock
        SET QuantityOnHand = QuantityOnHand + @Delta,
            ModifiedAtUtc = SYSUTCDATETIME()
        OUTPUT inserted.QuantityOnHand INTO @Updated
        WHERE BranchCode = @BranchCode
          AND Sku = @Sku
          AND QuantityOnHand + @Delta >= 0;

        IF NOT EXISTS (SELECT 1 FROM @Updated)
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.BranchStock WHERE BranchCode = @BranchCode AND Sku = @Sku)
                THROW 50006, N'Branch stock cannot go negative.', 1;
            THROW 50404, N'This product is not stocked at this branch.', 1;
        END;

        INSERT dbo.StockAdjustments (BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy, CreatedBy)
        SELECT @BranchCode, @Sku, @Delta, QuantityOnHand, @Reason, @ChangedBy, @ChangedBy
        FROM @Updated;

        COMMIT TRANSACTION;

        SELECT QuantityOnHand FROM @Updated;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
