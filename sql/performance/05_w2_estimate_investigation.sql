-- Why W2's date estimate moves between identical data sets. See "W2's estimate" in PERFORMANCE.md.
-- Demo database only: it replaces the date statistic and disables then rebuilds a workload index.
USE ApexPostDb;
SET NOCOUNT ON;

DECLARE @Date date = DATEADD(day, -1, CAST(SYSUTCDATETIME() AS date));
DECLARE @Stat sysname =
(
    SELECT TOP (1) s.name
    FROM sys.stats AS s
    INNER JOIN sys.stats_columns AS sc ON sc.object_id = s.object_id AND sc.stats_id = s.stats_id AND sc.stats_column_id = 1
    INNER JOIN sys.columns AS c ON c.object_id = sc.object_id AND c.column_id = sc.column_id
    WHERE s.object_id = OBJECT_ID('dbo.CounterTransactions') AND c.name = 'BusinessDate'
);
DECLARE @Update nvarchar(300) = N'UPDATE STATISTICS dbo.CounterTransactions ' + QUOTENAME(@Stat);
DECLARE @Show nvarchar(300) = N'DBCC SHOW_STATISTICS(''dbo.CounterTransactions'', ' + QUOTENAME(@Stat) + N') WITH HISTOGRAM, NO_INFOMSGS';

CREATE TABLE #Histogram (RangeHiKey date, RangeRows float, EqRows float, DistinctRangeRows float, AvgRangeRows float);
CREATE TABLE #Results (Run varchar(40), Steps int, MinEqRows int, MedianEqRows int, MaxEqRows int);

PRINT 'One date''s rows and the index pages that hold them';
SELECT COUNT(*) AS RowsOnDate, COUNT(DISTINCT p.page_id) AS IndexPages
FROM dbo.CounterTransactions AS t WITH (INDEX(IX_CounterTransactions_Branch_Date))
CROSS APPLY sys.fn_PhysLocCracker(t.%%physloc%%) AS p
WHERE t.BusinessDate = @Date;

EXEC (@Update);
INSERT #Histogram EXEC (@Show);
INSERT #Results
SELECT TOP (1) 'Sampled, index present', COUNT(*) OVER (), MIN(EqRows) OVER (), PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY EqRows) OVER (), MAX(EqRows) OVER () FROM #Histogram;
TRUNCATE TABLE #Histogram;

ALTER INDEX IX_CounterTransactions_Branch_Date ON dbo.CounterTransactions DISABLE;
EXEC (@Update);
INSERT #Histogram EXEC (@Show);
INSERT #Results
SELECT TOP (1) 'Sampled, index disabled', COUNT(*) OVER (), MIN(EqRows) OVER (), PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY EqRows) OVER (), MAX(EqRows) OVER () FROM #Histogram;
TRUNCATE TABLE #Histogram;
ALTER INDEX IX_CounterTransactions_Branch_Date ON dbo.CounterTransactions REBUILD;

SET @Update += N' WITH FULLSCAN';
EXEC (@Update);
INSERT #Histogram EXEC (@Show);
INSERT #Results
SELECT TOP (1) 'FULLSCAN', COUNT(*) OVER (), MIN(EqRows) OVER (), PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY EqRows) OVER (), MAX(EqRows) OVER () FROM #Histogram;

PRINT 'Rows stored per histogram step for BusinessDate';
SELECT Run, Steps, MinEqRows, MedianEqRows, MaxEqRows FROM #Results;
