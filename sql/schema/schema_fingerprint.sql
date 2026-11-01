-- Prints one hash of the schema's structure, so two databases, or one database before and after a rollback,
-- can be compared. Default constraints are compared by table and column, because SQL Server gives each one
-- an unrelated generated name. Run it against ApexPostDb with sqlcmd -I. See "Migration review" in DESIGN.md.
SET NOCOUNT ON;
DECLARE @x TABLE (v nvarchar(max) COLLATE DATABASE_DEFAULT);
INSERT @x SELECT 'C:'+t.name+'.'+c.name+':'+ty.name+':'+CAST(c.max_length AS varchar)+':'+CAST(c.precision AS varchar)+':'+CAST(c.scale AS varchar)+':'+CAST(c.is_nullable AS varchar) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.name<>'__EFMigrationsHistory';
INSERT @x SELECT 'I:'+OBJECT_NAME(i.object_id)+'.'+i.name+':'+CAST(i.is_unique AS varchar)+':'+ISNULL(i.filter_definition,'') FROM sys.indexes i WHERE i.name IS NOT NULL AND OBJECTPROPERTY(i.object_id,'IsUserTable')=1 AND OBJECT_NAME(i.object_id)<>'__EFMigrationsHistory';
INSERT @x SELECT 'K:'+OBJECT_NAME(parent_object_id)+'.'+name+':'+definition FROM sys.check_constraints;
INSERT @x SELECT 'D:'+OBJECT_NAME(parent_object_id)+'.'+COL_NAME(parent_object_id,parent_column_id)+':'+definition FROM sys.default_constraints;
INSERT @x SELECT 'F:'+OBJECT_NAME(parent_object_id)+'.'+name+':'+delete_referential_action_desc FROM sys.foreign_keys;
INSERT @x SELECT 'P:'+name+':'+CONVERT(varchar(64),HASHBYTES('SHA2_256',OBJECT_DEFINITION(object_id)),2) FROM sys.procedures;
INSERT @x SELECT 'R:'+CAST(Id AS varchar)+':'+Name FROM dbo.Roles;
INSERT @x SELECT 'Z:'+CAST(COUNT(*) AS varchar) FROM dbo.ShippingRates;
INSERT @x SELECT 'Y:'+CAST(COUNT(*) AS varchar) FROM dbo.ShippingZones;
INSERT @x SELECT 'G:'+CAST(COUNT(*) AS varchar) FROM dbo.ProductCategories;
SELECT CONVERT(varchar(64), HASHBYTES('SHA2_256', (SELECT STRING_AGG(v,'|') WITHIN GROUP (ORDER BY v) FROM @x)),2) AS fingerprint, (SELECT COUNT(*) FROM @x) AS facts, (SELECT COUNT(*) FROM sys.tables WHERE name<>'__EFMigrationsHistory') AS tables, (SELECT COUNT(*) FROM sys.procedures) AS procs, (SELECT COUNT(*) FROM __EFMigrationsHistory) AS migrations;
