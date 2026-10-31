-- Storage cost of each index, and how often it is read versus written since the last restart.
USE ApexPostDb;

SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    SUM(ps.used_page_count) * 8 AS UsedKb,
    SUM(ps.row_count) AS RowsInIndex,
    MAX(COALESCE(u.user_seeks, 0) + COALESCE(u.user_scans, 0) + COALESCE(u.user_lookups, 0)) AS Reads,
    MAX(COALESCE(u.user_updates, 0)) AS Writes
FROM sys.indexes AS i
INNER JOIN sys.dm_db_partition_stats AS ps ON ps.object_id = i.object_id AND ps.index_id = i.index_id
LEFT JOIN sys.dm_db_index_usage_stats AS u
    ON u.object_id = i.object_id AND u.index_id = i.index_id AND u.database_id = DB_ID()
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
GROUP BY i.object_id, i.name
ORDER BY TableName, IndexName;

-- Write overhead: run on a demo database with and without the indexes (02_toggle_indexes.sql) and compare the elapsed time.
SET STATISTICS TIME ON;
BEGIN TRANSACTION;
UPDATE dbo.BranchStock SET QuantityOnHand = QuantityOnHand + 1;
ROLLBACK TRANSACTION;
SET STATISTICS TIME OFF;
