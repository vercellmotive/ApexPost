-- The four workloads. Run with "Include Actual Execution Plan" on, and save each plan as a .sqlplan in docs/plans.
-- Run it twice: before and after applying the AddWorkloadIndexes migration. See 02_toggle_indexes.sql.
USE ApexPostDb;
SET STATISTICS IO, TIME ON;

DECLARE @Branch varchar(10) = 'B001';
DECLARE @Date date = DATEADD(day, -1, CAST(SYSUTCDATETIME() AS date));

PRINT 'W1 low-stock report by branch';
SELECT s.BranchCode, s.Sku, s.QuantityOnHand
FROM dbo.BranchStock AS s
WHERE s.BranchCode = @Branch AND s.QuantityOnHand <= 5
OPTION (RECOMPILE);

PRINT 'W2 daily sales by branch and date';
SELECT t.BranchCode, SUM(l.LineTotal) AS Total
FROM dbo.CounterTransactions AS t
INNER JOIN dbo.TransactionLines AS l ON l.TransactionId = t.Id
WHERE t.BranchCode = @Branch AND t.BusinessDate = @Date AND t.Status = 'Paid'
GROUP BY t.BranchCode
OPTION (RECOMPILE);

PRINT 'W3 shipment search by zone and service level';
SELECT COUNT_BIG(*) AS Shipments
FROM dbo.ShippingRates AS r
INNER JOIN dbo.Shipments AS s ON s.ShippingRateId = r.Id
WHERE r.ZoneId = 'Zone 3' AND r.ServiceLevel = 'Express'
OPTION (RECOMPILE);

PRINT 'W4 high-value money-order search for auditors';
SELECT m.Amount, m.Fee, m.TransactionLineId
FROM dbo.MoneyOrders AS m
WHERE m.Amount > 1000
OPTION (RECOMPILE);

SET STATISTICS IO, TIME OFF;
