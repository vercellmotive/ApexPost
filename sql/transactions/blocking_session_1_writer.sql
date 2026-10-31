-- SESSION 1 of the blocking demo. Run in its own SSMS window, one step at a time, using a demo database only.
USE ApexPostDb;

-- Step 1: open a transaction and hold an exclusive row lock on the stock row. Leave it uncommitted.
BEGIN TRANSACTION;
UPDATE dbo.BranchStock
SET QuantityOnHand = QuantityOnHand + 1
WHERE BranchCode = 'B001' AND Sku = 'SKU-001';

-- Step 2: run sql/transactions/blocking_session_2_reader.sql in a second window. It should wait.

-- Step 3: run this line in a third window to see who is blocking whom, and record the output in PERFORMANCE.md.
-- SELECT session_id, blocking_session_id, wait_type, wait_time, command FROM sys.dm_exec_requests WHERE blocking_session_id <> 0;

-- Step 4: release the lock, and session 2 returns immediately.
ROLLBACK TRANSACTION;
