-- Products at or below @Threshold, optionally for one branch.
-- OPTION (RECOMPILE) is deliberate: one cached plan cannot suit both "one branch" (a seek) and "all branches"
-- (a scan). The report runs rarely enough that compile cost is acceptable. See PERFORMANCE.md for the alternatives.
CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
    @BranchCode varchar(10) = NULL,
    @Threshold int
AS
BEGIN
    SET NOCOUNT ON;

    IF @Threshold IS NULL OR @Threshold < 0
        THROW 50002, N'@Threshold must be zero or greater.', 1;

    IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE BranchCode = @BranchCode)
        THROW 50404, N'The branch was not found.', 1;

    SELECT s.BranchCode, s.Sku, p.Name AS ProductName, s.QuantityOnHand
    FROM dbo.BranchStock AS s
    INNER JOIN dbo.Products AS p ON p.Sku = s.Sku
    WHERE s.QuantityOnHand <= @Threshold
      AND (@BranchCode IS NULL OR s.BranchCode = @BranchCode)
    ORDER BY s.BranchCode, s.QuantityOnHand, s.Sku
    OPTION (RECOMPILE);
END
