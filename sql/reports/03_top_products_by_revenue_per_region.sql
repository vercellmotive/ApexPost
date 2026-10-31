-- Top 10 products by revenue in each region, leaving out products that earned nothing.
WITH RegionRevenue AS
(
    SELECT
        b.Region,
        p.Sku,
        p.Name AS ProductName,
        SUM(l.LineTotal) AS Revenue,
        SUM(l.Quantity) AS UnitsSold
    FROM dbo.TransactionLines AS l
    INNER JOIN dbo.CounterTransactions AS t ON t.Id = l.TransactionId
    INNER JOIN dbo.Branches AS b ON b.BranchCode = t.BranchCode
    INNER JOIN dbo.Products AS p ON p.Sku = l.Sku
    WHERE t.Status = 'Paid'
      AND l.LineType = 'RetailProduct'
    GROUP BY b.Region, p.Sku, p.Name
    HAVING SUM(l.LineTotal) > 0
),
Ranked AS
(
    SELECT *, ROW_NUMBER() OVER (PARTITION BY Region ORDER BY Revenue DESC, Sku) AS RevenueRank
    FROM RegionRevenue
)
SELECT Region, RevenueRank, Sku, ProductName, Revenue, UnitsSold
FROM Ranked
WHERE RevenueRank <= 10
ORDER BY Region, RevenueRank;
