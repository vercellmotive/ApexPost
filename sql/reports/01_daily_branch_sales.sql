-- Daily branch sales split by retail and services. A money order's principal is not revenue, so only its fee counts.
DECLARE @Date date = CAST(SYSUTCDATETIME() AS date);

SELECT
    b.BranchCode,
    b.Name AS BranchName,
    SUM(CASE WHEN l.LineType = 'RetailProduct' THEN l.LineTotal ELSE 0 END) AS RetailTotal,
    SUM(CASE
        WHEN l.LineType = 'RetailProduct' THEN 0
        WHEN l.LineType = 'MoneyOrder' THEN m.Fee
        ELSE l.LineTotal END) AS ServicesTotal
FROM dbo.Branches AS b
INNER JOIN dbo.CounterTransactions AS t ON t.BranchCode = b.BranchCode
INNER JOIN dbo.TransactionLines AS l ON l.TransactionId = t.Id
LEFT JOIN dbo.MoneyOrders AS m ON m.TransactionLineId = l.Id
WHERE t.BusinessDate = @Date
  AND t.Status = 'Paid'
GROUP BY b.BranchCode, b.Name
ORDER BY b.BranchCode;
