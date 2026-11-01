-- Reporting role for a head-office or auditor tool. It can run the two report procedures, and nothing else:
-- no SELECT on any table, so it never sees money-order names, staff emails or masked documents.
-- Ownership chaining lets the procedures read the tables they need on the caller's behalf.
-- Create the login and its password outside source control, then: ALTER ROLE apexpost_reports ADD MEMBER [your_report_user];
IF DATABASE_PRINCIPAL_ID('apexpost_reports') IS NULL
    CREATE ROLE apexpost_reports;
GO

GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSales TO apexpost_reports;
GRANT EXECUTE ON OBJECT::dbo.usp_LowStockReport TO apexpost_reports;
GO
