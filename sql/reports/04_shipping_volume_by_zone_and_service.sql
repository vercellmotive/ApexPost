-- Shipping volume by zone and service level. Zone and level live on ShippingRates, so Shipments stays in 3NF.
SELECT
    z.Name AS Zone,
    r.ServiceLevel,
    SUM(l.Quantity) AS Shipments,
    SUM(s.WeightKg) AS TotalWeightKg
FROM dbo.Shipments AS s
INNER JOIN dbo.TransactionLines AS l ON l.Id = s.TransactionLineId
INNER JOIN dbo.CounterTransactions AS t ON t.Id = l.TransactionId
INNER JOIN dbo.ShippingRates AS r ON r.Id = s.ShippingRateId
INNER JOIN dbo.ShippingZones AS z ON z.Id = r.ZoneId
WHERE t.Status = 'Paid'
GROUP BY z.Name, r.ServiceLevel
ORDER BY z.Name, r.ServiceLevel;
