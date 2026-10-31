-- Baseline helper for a demo database: disables the workload indexes, then rebuilds them, without changing migrations.
-- Run only section A for baselines and only section B to restore. Never run this in production.
USE ApexPostDb;

-- Section A: disable (run before the baseline).
ALTER INDEX IX_BranchStock_LowStock ON dbo.BranchStock DISABLE;
ALTER INDEX IX_CounterTransactions_Branch_Date ON dbo.CounterTransactions DISABLE;
ALTER INDEX IX_ShippingRates_Zone_Level ON dbo.ShippingRates DISABLE;
ALTER INDEX IX_Shipments_Rate ON dbo.Shipments DISABLE;
ALTER INDEX IX_MoneyOrders_HighValue ON dbo.MoneyOrders DISABLE;
GO

-- Section B: rebuild (run before the "after" measurement).
ALTER INDEX IX_BranchStock_LowStock ON dbo.BranchStock REBUILD;
ALTER INDEX IX_CounterTransactions_Branch_Date ON dbo.CounterTransactions REBUILD;
ALTER INDEX IX_ShippingRates_Zone_Level ON dbo.ShippingRates REBUILD;
ALTER INDEX IX_Shipments_Rate ON dbo.Shipments REBUILD;
ALTER INDEX IX_MoneyOrders_HighValue ON dbo.MoneyOrders REBUILD;
GO
