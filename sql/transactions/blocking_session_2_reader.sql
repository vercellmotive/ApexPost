-- SESSION 2 of the blocking demo. Start this while session 1 holds its transaction open.
USE ApexPostDb;

-- EF Core turns on READ_COMMITTED_SNAPSHOT when it creates a database, so check that first.
SELECT name, is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME();

-- Snapshot-based READ COMMITTED: returns at once with the last committed value, without waiting.
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT QuantityOnHand AS CommittedSnapshot FROM dbo.BranchStock WHERE BranchCode = 'B001' AND Sku = 'SKU-001';

-- Locking READ UNCOMMITTED: returns at once, but with a value session 1 may yet roll back.
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SELECT QuantityOnHand AS DirtyRead FROM dbo.BranchStock WHERE BranchCode = 'B001' AND Sku = 'SKU-001';

-- Locking READ COMMITTED: this one blocks with an LCK_M_S wait until session 1 finishes.
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT QuantityOnHand AS LockingRead FROM dbo.BranchStock WITH (READCOMMITTEDLOCK) WHERE BranchCode = 'B001' AND Sku = 'SKU-001';
