-- Retail vs services totals for one trading day. @BranchCode = NULL returns every branch (the auditor view).
-- A money order's principal is customer money, so only its fee counts as service revenue.
CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
    @BranchCode varchar(10) = NULL,
    @Date date
AS
BEGIN
    SET NOCOUNT ON;

    IF @Date IS NULL
        THROW 50001, N'@Date is required.', 1;

    IF @BranchCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE BranchCode = @BranchCode)
        THROW 50404, N'The branch was not found.', 1;

    SELECT
        t.BranchCode,
        CAST(SUM(CASE WHEN l.LineType = 'RetailProduct' THEN l.LineTotal ELSE 0 END) AS decimal(18,2)) AS RetailTotal,
        CAST(SUM(CASE
            WHEN l.LineType = 'RetailProduct' THEN 0
            WHEN l.LineType = 'MoneyOrder' THEN m.Fee
            ELSE l.LineTotal END) AS decimal(18,2)) AS ServicesTotal
    FROM dbo.CounterTransactions AS t
    INNER JOIN dbo.TransactionLines AS l ON l.TransactionId = t.Id
    LEFT JOIN dbo.MoneyOrders AS m ON m.TransactionLineId = l.Id
    WHERE t.BusinessDate = @Date
      AND t.Status = 'Paid'
      AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
    GROUP BY t.BranchCode
    ORDER BY t.BranchCode
    OPTION (RECOMPILE);
END
