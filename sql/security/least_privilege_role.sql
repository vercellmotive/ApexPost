-- Least-privilege role for the application login. It holds no DDL rights: migrations run under a separate deployment login.
-- Create the login and its password outside source control, then: ALTER ROLE apexpost_app ADD MEMBER [your_app_user];
IF DATABASE_PRINCIPAL_ID('apexpost_app') IS NULL
    CREATE ROLE apexpost_app;
GO

GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_DailyBranchSales TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_LowStockReport TO apexpost_app;
GRANT EXECUTE ON OBJECT::dbo.usp_AdjustStock TO apexpost_app;

-- The audit trail is append-only for the application.
DENY UPDATE, DELETE ON OBJECT::dbo.AuditEntries TO apexpost_app;
DENY DELETE ON SCHEMA::dbo TO apexpost_app;
GO
