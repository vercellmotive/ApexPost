-- SESSION 1 of the deadlock demo. Start this, then start session 2 within 5 seconds, on a demo database only.
USE ApexPostDb;

-- This order is deliberately the reverse of session 2's. The app never does this: see PERFORMANCE.md.
BEGIN TRANSACTION;
UPDATE dbo.BranchStock SET QuantityOnHand = QuantityOnHand + 1 WHERE BranchCode = 'B001' AND Sku = 'SKU-001';
WAITFOR DELAY '00:00:05';
UPDATE dbo.BranchStock SET QuantityOnHand = QuantityOnHand + 1 WHERE BranchCode = 'B001' AND Sku = 'SKU-002';
SELECT 'Session 1 survived' AS Outcome;
ROLLBACK TRANSACTION;
