-- Non-SARGable vs SARGable. Wrapping the column in YEAR() stops the optimizer seeking on BusinessDate.
USE ApexPostDb;
SET STATISTICS IO, TIME ON;

DECLARE @Branch varchar(10) = 'B001';

-- Before: the function must run for every row of the branch.
SELECT COUNT_BIG(*) AS Sales
FROM dbo.CounterTransactions
WHERE BranchCode = @Branch AND YEAR(BusinessDate) = 2026;

-- After: a half-open range keeps both key columns of the index seekable.
SELECT COUNT_BIG(*) AS Sales
FROM dbo.CounterTransactions
WHERE BranchCode = @Branch
  AND BusinessDate >= '20260101'
  AND BusinessDate < '20270101';

SET STATISTICS IO, TIME OFF;
