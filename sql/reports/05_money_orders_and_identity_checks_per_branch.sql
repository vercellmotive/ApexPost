-- Auditor view. LEFT JOINs keep branches with no money orders or checks, which shows up as zero rather than missing.
SELECT
    b.BranchCode,
    b.Name AS BranchName,
    COUNT(m.Id) AS MoneyOrders,
    COALESCE(SUM(m.Amount), 0) AS MoneyOrderValue,
    COUNT(v.Id) AS IdentityVerifications,
    SUM(CASE WHEN v.IsVerified = 1 THEN 1 ELSE 0 END) AS VerificationsPassed
FROM dbo.Branches AS b
LEFT JOIN dbo.CounterTransactions AS t ON t.BranchCode = b.BranchCode
LEFT JOIN dbo.TransactionLines AS l ON l.TransactionId = t.Id
LEFT JOIN dbo.MoneyOrders AS m ON m.TransactionLineId = l.Id
LEFT JOIN dbo.IdentityVerifications AS v ON v.TransactionLineId = l.Id
GROUP BY b.BranchCode, b.Name
ORDER BY b.BranchCode;
