-- Investigates estimated vs actual rows. Compare "Estimated Number of Rows" with "Actual Number of Rows" on the plans.
USE ApexPostDb;
GO

-- Parameter sensitivity: this plan is compiled for the first value it sees, and is reused for the other.
-- SQL Server 2022's Parameter Sensitive Plan optimization (compatibility level 160) can hide this by caching
-- a plan per value range, so it is switched off for the demo and restored at the end.
ALTER DATABASE SCOPED CONFIGURATION SET PARAMETER_SENSITIVE_PLAN_OPTIMIZATION = OFF;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Demo_TransactionsByStatus @Status varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(*) AS Matches, MIN(OccurredAtUtc) AS FirstSale
    FROM dbo.CounterTransactions
    WHERE Status = @Status;
END
GO

EXEC sp_recompile N'dbo.usp_Demo_TransactionsByStatus';
EXEC dbo.usp_Demo_TransactionsByStatus @Status = 'Refunded'; -- compiles for a rare value
EXEC dbo.usp_Demo_TransactionsByStatus @Status = 'Paid';     -- reuses that plan for the common value
GO

-- Stale statistics: these show how old the statistics are and how many rows have changed since.
SELECT s.name AS StatisticsName, p.last_updated, p.rows, p.rows_sampled, p.modification_counter
FROM sys.stats AS s
CROSS APPLY sys.dm_db_stats_properties(s.object_id, s.stats_id) AS p
WHERE s.object_id = OBJECT_ID(N'dbo.CounterTransactions');
GO

DROP PROCEDURE dbo.usp_Demo_TransactionsByStatus;
GO

ALTER DATABASE SCOPED CONFIGURATION SET PARAMETER_SENSITIVE_PLAN_OPTIMIZATION = ON;
GO
