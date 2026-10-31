-- The LEFT JOIN keeps every active branch, and HAVING keeps those that matched no paid transaction that day.
-- The date test belongs in ON: in WHERE it would remove the unmatched rows and turn this into an inner join.
DECLARE @Date date = CAST(SYSUTCDATETIME() AS date);

SELECT b.BranchCode, b.Name, b.Region
FROM dbo.Branches AS b
LEFT JOIN dbo.CounterTransactions AS t
    ON t.BranchCode = b.BranchCode
   AND t.BusinessDate = @Date
   AND t.Status = 'Paid'
WHERE b.IsActive = 1
GROUP BY b.BranchCode, b.Name, b.Region
HAVING COUNT(t.Id) = 0
ORDER BY b.Region, b.BranchCode;
