-- SESSION 2 of the blocking demo. Start this while session 1 holds its transaction open.
USE ApexPostDb;

-- READ COMMITTED (the default) with locking: the shared lock request waits for session 1's exclusive lock.
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT QuantityOnHand FROM dbo.BranchStock WHERE BranchCode = 'B001' AND Sku = 'SKU-001';

-- For comparison after the first read returns: READ UNCOMMITTED returns at once, but can show a value that is rolled back.
-- SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
-- SELECT QuantityOnHand FROM dbo.BranchStock WHERE BranchCode = 'B001' AND Sku = 'SKU-001';

SELECT name, is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME();
