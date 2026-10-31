-- SESSION 2 of the deadlock demo. LOW priority makes this session the victim, so it should get error 1205.
USE ApexPostDb;

SET DEADLOCK_PRIORITY LOW;
BEGIN TRANSACTION;
UPDATE dbo.BranchStock SET QuantityOnHand = QuantityOnHand + 1 WHERE BranchCode = 'B001' AND Sku = 'SKU-002';
WAITFOR DELAY '00:00:05';
UPDATE dbo.BranchStock SET QuantityOnHand = QuantityOnHand + 1 WHERE BranchCode = 'B001' AND Sku = 'SKU-001';
SELECT 'Session 2 survived' AS Outcome;
ROLLBACK TRANSACTION;
