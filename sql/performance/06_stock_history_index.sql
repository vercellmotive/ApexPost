-- W5: the stock history of one product at one branch, newest first.
-- Run on a seeded demo database that has no stock history yet. It adds fictional adjustment rows, measures the
-- lookup with the history index disabled and then rebuilt, and times inserts both ways. Never run this in production.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Rows int = 300000;
IF NOT EXISTS (SELECT 1 FROM dbo.BranchStock)
    THROW 50010, N'Seed the database first: no stock rows exist.', 1;
IF (SELECT COUNT(*) FROM dbo.StockAdjustments) > 1000
    THROW 50011, N'This database already has stock history. Use a freshly seeded demo database.', 1;

DROP TABLE IF EXISTS #Stock;
SELECT ROW_NUMBER() OVER (ORDER BY BranchCode, Sku) - 1 AS RowIndex, BranchCode, Sku
INTO #Stock
FROM dbo.BranchStock;
DECLARE @StockCount int = (SELECT COUNT(*) FROM #Stock);

;WITH Numbers AS (
    SELECT TOP (@Rows) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b)
INSERT dbo.StockAdjustments (BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy, ChangedAtUtc)
SELECT s.BranchCode, s.Sku, 1, 10, 'Replenishment', 'STF001', DATEADD(MINUTE, -numbers.n, SYSUTCDATETIME())
FROM Numbers AS numbers
INNER JOIN #Stock AS s ON s.RowIndex = numbers.n % @StockCount;
UPDATE STATISTICS dbo.StockAdjustments WITH FULLSCAN;

SELECT COUNT(*) AS HistoryRows, COUNT(DISTINCT CONCAT(BranchCode, '/', Sku)) AS BranchProductPairs FROM dbo.StockAdjustments;

-- A: the lookup with the history index disabled. Each set runs twice and the second run is the one to keep, so the cache is warm.
ALTER INDEX IX_StockAdjustments_Branch_Sku_Time ON dbo.StockAdjustments DISABLE;
PRINT 'A: history index disabled';
SET STATISTICS IO ON; SET STATISTICS TIME ON;
SELECT TOP (50) Id, Delta, QuantityAfter, Reason, ChangedBy, ChangedAtUtc
FROM dbo.StockAdjustments WHERE BranchCode = 'B001' AND Sku = 'SKU-001' ORDER BY ChangedAtUtc DESC;
SELECT TOP (50) Id, Delta, QuantityAfter, Reason, ChangedBy, ChangedAtUtc
FROM dbo.StockAdjustments WHERE BranchCode = 'B001' AND Sku = 'SKU-001' ORDER BY ChangedAtUtc DESC;
SET STATISTICS IO OFF; SET STATISTICS TIME OFF;

-- Write cost without the index: insert 100,000 rows and roll back, three times.
DECLARE @run int = 1, @start datetime2, @ms int;
WHILE @run <= 3
BEGIN
    BEGIN TRANSACTION;
    SET @start = SYSDATETIME();
    ;WITH Numbers AS (SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b)
    INSERT dbo.StockAdjustments (BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy)
    SELECT s.BranchCode, s.Sku, 1, 10, 'Replenishment', 'STF001' FROM Numbers AS numbers INNER JOIN #Stock AS s ON s.RowIndex = numbers.n % @StockCount;
    SET @ms = DATEDIFF(MILLISECOND, @start, SYSDATETIME());
    ROLLBACK TRANSACTION;
    PRINT CONCAT('insert of 100000 rows without the index, run ', @run, ': ', @ms, ' ms');
    SET @run += 1;
END;

-- B: rebuild the index and repeat.
ALTER INDEX IX_StockAdjustments_Branch_Sku_Time ON dbo.StockAdjustments REBUILD;
PRINT 'B: history index rebuilt';
SET STATISTICS IO ON; SET STATISTICS TIME ON;
SELECT TOP (50) Id, Delta, QuantityAfter, Reason, ChangedBy, ChangedAtUtc
FROM dbo.StockAdjustments WHERE BranchCode = 'B001' AND Sku = 'SKU-001' ORDER BY ChangedAtUtc DESC;
SELECT TOP (50) Id, Delta, QuantityAfter, Reason, ChangedBy, ChangedAtUtc
FROM dbo.StockAdjustments WHERE BranchCode = 'B001' AND Sku = 'SKU-001' ORDER BY ChangedAtUtc DESC;
SET STATISTICS IO OFF; SET STATISTICS TIME OFF;

SET @run = 1;
WHILE @run <= 3
BEGIN
    BEGIN TRANSACTION;
    SET @start = SYSDATETIME();
    ;WITH Numbers AS (SELECT TOP (100000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b)
    INSERT dbo.StockAdjustments (BranchCode, Sku, Delta, QuantityAfter, Reason, ChangedBy)
    SELECT s.BranchCode, s.Sku, 1, 10, 'Replenishment', 'STF001' FROM Numbers AS numbers INNER JOIN #Stock AS s ON s.RowIndex = numbers.n % @StockCount;
    SET @ms = DATEDIFF(MILLISECOND, @start, SYSDATETIME());
    ROLLBACK TRANSACTION;
    PRINT CONCAT('insert of 100000 rows with the index, run ', @run, ': ', @ms, ' ms');
    SET @run += 1;
END;

-- Storage cost of each index on the table.
SELECT i.name AS IndexName, SUM(ps.used_page_count) * 8 AS SizeKb, MAX(ps.row_count) AS Rows
FROM sys.dm_db_partition_stats AS ps
INNER JOIN sys.indexes AS i ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE ps.object_id = OBJECT_ID('dbo.StockAdjustments')
GROUP BY i.name
ORDER BY i.name;
