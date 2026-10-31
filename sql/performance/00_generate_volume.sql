-- Adds fictional rows so the indexes have realistic data to work on. Run on a demo database after seeding.
-- Set @Rows to the volume to test, and record it in PERFORMANCE.md. Set-based, so it should run in one batch.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Rows int = 200000;

IF NOT EXISTS (SELECT 1 FROM dbo.Counters)
    THROW 50010, N'Seed the database first: no counters exist.', 1;

-- OFFSET cannot reference an outer column, so counters and products are numbered once and joined by remainder.
DROP TABLE IF EXISTS #Counters;
SELECT ROW_NUMBER() OVER (ORDER BY Id) - 1 AS RowIndex, BranchCode, TerminalCode
INTO #Counters
FROM dbo.Counters;

DROP TABLE IF EXISTS #Products;
SELECT ROW_NUMBER() OVER (ORDER BY Id) - 1 AS RowIndex, Sku, Name, UnitPrice
INTO #Products
FROM dbo.Products;

DECLARE @CounterCount int = (SELECT COUNT(*) FROM #Counters);
DECLARE @ProductCount int = (SELECT COUNT(*) FROM #Products);

DROP TABLE IF EXISTS #Sales;
WITH Numbers AS
(
    SELECT TOP (@Rows) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b
)
SELECT
    NEWID() AS TransactionId,
    NEWID() AS LineId,
    NEWID() AS PaymentId,
    n,
    DATEADD(day, -(n % 730), CAST(SYSUTCDATETIME() AS date)) AS BusinessDate
INTO #Sales
FROM Numbers;

BEGIN TRANSACTION;

INSERT dbo.CounterTransactions (Id, BranchCode, CounterCode, ClerkStaffId, Status, OccurredAtUtc, BusinessDate, InventoryRestocked)
SELECT s.TransactionId, c.BranchCode, c.TerminalCode, st.Id,
       CASE WHEN s.n % 20 = 0 THEN 'Voided' ELSE 'Paid' END,
       CAST(s.BusinessDate AS datetime2), s.BusinessDate, 0
FROM #Sales AS s
INNER JOIN #Counters AS c ON c.RowIndex = s.n % @CounterCount
CROSS APPLY (SELECT TOP (1) Id FROM dbo.Staff WHERE HomeBranchCode = c.BranchCode ORDER BY Id) AS st;

INSERT dbo.TransactionLines (Id, TransactionId, LineNumber, Description, LineType, Quantity, UnitPrice, TaxCategory, TaxAmount, LineTotal, Sku)
SELECT s.LineId, s.TransactionId, 1, p.Name, 'RetailProduct', 1, p.UnitPrice, 'StandardGst', ROUND(p.UnitPrice * 0.10, 2), p.UnitPrice + ROUND(p.UnitPrice * 0.10, 2), p.Sku
FROM #Sales AS s
INNER JOIN #Products AS p ON p.RowIndex = s.n % @ProductCount;

INSERT dbo.Shipments (Id, TransactionLineId, ShippingRateId, WeightKg, LengthCm, WidthCm, HeightCm)
SELECT NEWID(), s.LineId, (s.n % 60) + 1, 0.25, 30, 20, 10
FROM #Sales AS s
WHERE s.n % 4 = 0;

INSERT dbo.MoneyOrders (Id, TransactionLineId, Amount, Fee, Sender, Recipient)
SELECT NEWID(), s.LineId, CASE WHEN s.n % 50 = 0 THEN 1500 ELSE 200 END, 10, N'Demo Sender', N'Demo Recipient'
FROM #Sales AS s
WHERE s.n % 10 = 0;

COMMIT TRANSACTION;

UPDATE STATISTICS dbo.CounterTransactions;
UPDATE STATISTICS dbo.TransactionLines;
UPDATE STATISTICS dbo.Shipments;
UPDATE STATISTICS dbo.MoneyOrders;

SELECT
    (SELECT COUNT_BIG(*) FROM dbo.CounterTransactions) AS Transactions,
    (SELECT COUNT_BIG(*) FROM dbo.TransactionLines) AS Lines,
    (SELECT COUNT_BIG(*) FROM dbo.Shipments) AS Shipments,
    (SELECT COUNT_BIG(*) FROM dbo.MoneyOrders) AS MoneyOrders;
