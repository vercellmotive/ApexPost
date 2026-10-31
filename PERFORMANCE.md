# ApexPost performance evidence

## Status: measurements still needed

**No execution plans, logical reads or CPU times have been captured yet, so this file contains none.** The project was built without access to a SQL Server, and the assignment forbids invented numbers. The scripts, indexes and result tables are ready, and running them takes one SQL Server and about 30 minutes. Each `NOT MEASURED` cell below is a value to fill in from your own run.

| Evidence | State |
|---|---|
| Workload indexes, in code and in a migration | Done |
| Generated SQL from `ToQueryString()` | Done: captured from EF Core below |
| Scripts for volume, workloads, SARGability, estimates and index cost | Done: all pass the T-SQL parser |
| Baseline and after measurements, plans, write cost, storage | **Not measured** |
| Blocking observation, estimate mismatch | **Not observed** |

Anything marked **Expected** comes from how SQL Server is documented to work. It is a prediction to test, not a result.

## How to capture the evidence

Use a demo database. SQL Server Developer or Express editions and SSMS are free.

1. Create and seed the database by following `README.md`.
2. Run `sql/performance/00_generate_volume.sql` and record the row counts below.
3. Run section A of `sql/performance/02_toggle_indexes.sql` to disable the workload indexes.
4. In SSMS, turn on **Include Actual Execution Plan**, run `sql/performance/01_workload_queries.sql`, and record the reads and CPU from the Messages tab. Save each plan to `docs/plans`.
5. Run section B of `02_toggle_indexes.sql`, then repeat step 4.
6. Run `03_non_sargable_fix.sql`, `04_estimate_mismatch.sql` and `05_index_cost.sql`, and record the results.
7. Replace each `NOT MEASURED` cell, delete the status section above, and write your conclusions.

**Test volume:** transactions `NOT MEASURED`, lines `NOT MEASURED`, shipments `NOT MEASURED`, money orders `NOT MEASURED`.

If a small dataset still produces scans, leave that result in and say so: on a table of a few pages, a scan can honestly be the cheapest plan.

## Workloads and index design

Indexes are defined in code and deployed by the `AddWorkloadIndexes` migration. Key columns come from `[Index]` attributes on persistence records. `[Index]` has no option for `INCLUDE` or filters, so those come from Fluent `HasIndex` on the same named index. `BranchStock` maps a Domain class, and an attribute there would make Domain depend on EF Core, so its index is entirely Fluent (ADR-26).

### W1: low-stock report by branch

**Index:** `IX_BranchStock_Branch_Quantity (BranchCode, QuantityOnHand) INCLUDE (Sku)`

**Why this order:** the query tests `BranchCode` for equality and `QuantityOnHand` as a range, so the equality column goes first and the range can seek within one branch. `INCLUDE (Sku)` covers the query without a key lookup, and needs no sorting by itself.

**Expected:** before, a seek on `UQ_BranchStock_Branch_Sku` reading the whole branch plus key lookups, or a clustered scan. After, a seek reading only the low rows. Because `BranchStock` is updated on every sale, this index adds write work on each one.

| Measure | Before | After |
|---|---|---|
| Plan shape | NOT MEASURED | NOT MEASURED |
| Logical reads | NOT MEASURED | NOT MEASURED |
| CPU time (ms) | NOT MEASURED | NOT MEASURED |

### W2: daily sales by branch and date

**Index:** `IX_CounterTransactions_Branch_Date (BranchCode, BusinessDate) INCLUDE (Status)`

**Why this order:** both are equality tests, and `BranchCode` first also serves "all of a branch's recent days". `Status` is included rather than keyed because it has few distinct values and most rows are `Paid`. Lines are reached through the existing foreign key index on `TransactionId`.

**Expected:** before, a clustered scan of `CounterTransactions`. After, a seek followed by a nested loop into `TransactionLines`.

| Measure | Before | After |
|---|---|---|
| Plan shape | NOT MEASURED | NOT MEASURED |
| Logical reads | NOT MEASURED | NOT MEASURED |
| CPU time (ms) | NOT MEASURED | NOT MEASURED |

### W3: shipment search by zone and service level

**Indexes:** `IX_ShippingRates_Zone_Level (ZoneId, ServiceLevel)` and `IX_Shipments_Rate (ShippingRateId) INCLUDE (TransactionLineId)`

**Why this design:** zone and level are stored once, on `ShippingRates`, which keeps `Shipments` in 3NF. The search therefore resolves rates first, then seeks shipments by rate. `IX_Shipments_Rate` replaces the plain foreign key index on the same column.

**Expected:** `ShippingRates` has only 60 rows, so its index may make little measurable difference. If so, report that. The gain should come from `IX_Shipments_Rate`.

| Measure | Before | After |
|---|---|---|
| Plan shape | NOT MEASURED | NOT MEASURED |
| Logical reads | NOT MEASURED | NOT MEASURED |
| CPU time (ms) | NOT MEASURED | NOT MEASURED |

### W4: money-order search for auditors

**Index:** `IX_MoneyOrders_HighValue (Amount) INCLUDE (Fee, TransactionLineId) WHERE [Amount] > 1000`

**Why filtered:** auditors search high-value orders, a small part of the table. A filtered index holds only those rows, so it stays small and adds no write cost to ordinary orders. The filter matches the default identity threshold. If `Transactions:HighValueMoneyOrderThreshold` is lowered, queries below 1000 cannot use this index, and the filter needs a new migration.

**Expected:** before, a clustered scan. After, a scan of the much smaller filtered index, which already holds exactly the wanted rows.

| Measure | Before | After |
|---|---|---|
| Plan shape | NOT MEASURED | NOT MEASURED |
| Logical reads | NOT MEASURED | NOT MEASURED |
| CPU time (ms) | NOT MEASURED | NOT MEASURED |

## Write overhead and storage

From `sql/performance/05_index_cost.sql`.

| Index | Size (KB) | Reads | Writes |
|---|---|---|---|
| `IX_BranchStock_Branch_Quantity` | NOT MEASURED | NOT MEASURED | NOT MEASURED |
| `IX_CounterTransactions_Branch_Date` | NOT MEASURED | NOT MEASURED | NOT MEASURED |
| `IX_ShippingRates_Zone_Level` | NOT MEASURED | NOT MEASURED | NOT MEASURED |
| `IX_Shipments_Rate` | NOT MEASURED | NOT MEASURED | NOT MEASURED |
| `IX_MoneyOrders_HighValue` | NOT MEASURED | NOT MEASURED | NOT MEASURED |

| Bulk stock update | Without indexes | With indexes |
|---|---|---|
| Elapsed time (ms) | NOT MEASURED | NOT MEASURED |

## Fixing a non-SARGable predicate

`YEAR(BusinessDate) = 2026` applies a function to the column, so SQL Server must calculate it for every row in the branch. `BusinessDate >= '20260101' AND BusinessDate < '20270101'` leaves the column bare, so both index keys can seek. The application avoids the problem by design: `BusinessDate` is stored as a plain `date`.

| Measure | `YEAR()` | Date range |
|---|---|---|
| Plan shape | NOT MEASURED | NOT MEASURED |
| Logical reads | NOT MEASURED | NOT MEASURED |

## Estimated versus actual rows

`sql/performance/04_estimate_mismatch.sql` sets up parameter sensitivity: the procedure compiles for the rare `Refunded`, then reuses that plan for the common `Paid`. **Expected:** the plan for `Paid` shows a low estimate against a high actual count, because the estimate came from the first value. The script also shows statistics age, for checking stale statistics.

| Run | Estimated rows | Actual rows |
|---|---|---|
| `@Status = 'Refunded'` | NOT MEASURED | NOT MEASURED |
| `@Status = 'Paid'`, reusing the plan | NOT MEASURED | NOT MEASURED |

**Explanation:** NOT OBSERVED. Write what the plans show.

## Choosing OPTION (RECOMPILE) in usp_LowStockReport

`@BranchCode = NULL` means two different queries in one: one branch suits a seek, and all branches suits a scan. A single cached plan would be wrong for one of them.

| Option | Assessment |
|---|---|
| `OPTION (RECOMPILE)` (chosen) | Each run gets a plan for its actual values, and `NULL` branches are simplified away. It costs a compile per run, which is acceptable for an occasional report. |
| Two procedures, or an `IF` calling each | Stable plans without compiles, but more code to keep in step. Better if this ran many times a second. |
| `OPTIMIZE FOR UNKNOWN` | Avoids sniffing, but gives one average plan that suits neither case. |
| Dynamic SQL | Workable with `sp_executesql`, but harder to read and easy to get wrong. |

`usp_DailyBranchSales` has the same optional branch and makes the same choice.

## Blocking demonstration

Run `sql/transactions/blocking_session_1_writer.sql` and `blocking_session_2_reader.sql` in two SSMS windows.

**Expected:** under `READ COMMITTED` with locking, session 2 waits with a `LCK_M_S` wait while session 1 holds its exclusive row lock, then returns once session 1 rolls back. Under `READ UNCOMMITTED` it returns at once, and can show the uncommitted value.

**Observed:** NOT OBSERVED. Record the `sys.dm_exec_requests` output and the `is_read_committed_snapshot_on` value.

## How the design avoids deadlocks

- **Consistent order:** sales and stock procedures both touch `BranchStock` before anything else (`sql/transactions/record_sale_atomically.sql`, `usp_AdjustStock`). EF Core batches a sale in one `SaveChangesAsync`.
- **Short transactions:** optimistic concurrency holds no locks while a clerk works. Locks exist only during the commit, with no user interaction or remote calls inside it.
- **Sequenced work:** the courier rate call finishes before the save begins.

If a deadlock still happens, SQL Server reports error 1205, which `EnableRetryOnFailure` retries automatically.

## N+1 evidence

`ScreenQueries_DoNotIssueOneQueryPerRow` counts database commands with an interceptor, and needs SQL Server to run. `Microsoft.EntityFrameworkCore.Database.Command` logging is on in `appsettings.Development.json`, so each command also appears in the console when the app runs. **Result:** NOT MEASURED.

## Generated SQL

These are captured from EF Core with `ToQueryString()`, which needs no database. To refresh them, set `APEXPOST_SQL_CAPTURE_PATH` and run the test `ReadQueries_TranslateToSqlWithoutTracking`.

### Low stock (screen and dashboard)

```sql
DECLARE @threshold int = 5;
DECLARE @code varchar(10) = 'B001';

SELECT [b].[BranchCode], [b].[Sku], [p].[Name], [b].[QuantityOnHand]
FROM [BranchStock] AS [b]
INNER JOIN [Products] AS [p] ON [b].[Sku] = [p].[Sku]
WHERE [b].[QuantityOnHand] <= @threshold AND [b].[BranchCode] = @code
ORDER BY [b].[BranchCode], [b].[QuantityOnHand]
```

### Shipping volume by zone and service level

```sql
DECLARE @code varchar(10) = 'B001';

SELECT [s0].[ZoneId], [s0].[ServiceLevel], COALESCE(SUM([t].[Quantity]), 0)
FROM [Shipments] AS [s]
INNER JOIN [TransactionLines] AS [t] ON [s].[TransactionLineId] = [t].[Id]
INNER JOIN [CounterTransactions] AS [c] ON [t].[TransactionId] = [c].[Id]
INNER JOIN [ShippingRates] AS [s0] ON [s].[ShippingRateId] = [s0].[Id]
WHERE [c].[Status] = 'Paid' AND [c].[BranchCode] = @code
GROUP BY [s0].[ZoneId], [s0].[ServiceLevel]
ORDER BY [s0].[ZoneId], [s0].[ServiceLevel]
```

### Money orders and identity checks per branch

```sql
SELECT [c].[BranchCode], [t].[LineType], COUNT(*)
FROM [TransactionLines] AS [t]
INNER JOIN [CounterTransactions] AS [c] ON [t].[TransactionId] = [c].[Id]
WHERE [t].[LineType] IN ('MoneyOrder', 'IdentityVerification')
GROUP BY [c].[BranchCode], [t].[LineType]
```

### Branch transactions screen

```sql
DECLARE @p int = 200;
DECLARE @code varchar(10) = 'B001';

SELECT TOP(@p) [c].[Id], [c].[OccurredAtUtc], [c].[CounterCode], [c].[ClerkStaffId], (
    SELECT COUNT(*)
    FROM [TransactionLines] AS [t]
    WHERE [c].[Id] = [t].[TransactionId]), COALESCE((
    SELECT COALESCE(SUM([t0].[LineTotal]), 0.0)
    FROM [TransactionLines] AS [t0]
    WHERE [c].[Id] = [t0].[TransactionId]), 0.0), (
    SELECT TOP(1) [p].[Method]
    FROM [Payments] AS [p]
    WHERE [c].[Id] = [p].[TransactionId] AND [p].[Kind] = 'Sale'), [c].[Status], [c].[VoidReason]
FROM [CounterTransactions] AS [c]
WHERE [c].[BranchCode] = @code
ORDER BY [c].[OccurredAtUtc] DESC
```

### Branch catalogue screen

```sql
DECLARE @code varchar(10) = 'B001';

SELECT [p].[Sku], [p].[Name], [p0].[Name], [p].[UnitPrice], COALESCE((
    SELECT TOP(1) [b].[QuantityOnHand]
    FROM [BranchStock] AS [b]
    WHERE [b].[BranchCode] = @code AND [b].[Sku] = [p].[Sku]), 0)
FROM [Products] AS [p]
INNER JOIN [ProductCategories] AS [p0] ON [p].[CategoryId] = [p0].[Id]
ORDER BY [p].[Sku]
```

### Latest sales date

```sql
DECLARE @code varchar(10) = 'B001';

SELECT [c].[BusinessDate]
FROM [CounterTransactions] AS [c]
WHERE [c].[Status] = 'Paid' AND [c].[BranchCode] = @code
ORDER BY [c].[BusinessDate] DESC
```

### Staff list screen

```sql
SELECT [s].[Id], [s].[Name], [s].[Email], [s].[RoleId], [s].[HomeBranchCode], [s].[IsActive]
FROM [Staff] AS [s]
ORDER BY [s].[HomeBranchCode], [s].[Name]
```

### Staff detail screen

```sql
DECLARE @id varchar(20) = 'STF001';

SELECT [s].[Id], [s].[Name], [s].[Email], [s].[RoleId], [s].[HomeBranchCode], [s].[IsActive]
FROM [Staff] AS [s]
WHERE [s].[Id] = @id
ORDER BY [s].[HomeBranchCode], [s].[Name]
```

### Product detail screen

```sql
DECLARE @key varchar(20) = 'SKU-001';

SELECT [p].[Sku], [p].[Name], [p0].[Name], [p].[UnitPrice], [p].[TaxCategory]
FROM [Products] AS [p]
INNER JOIN [ProductCategories] AS [p0] ON [p].[CategoryId] = [p0].[Id]
WHERE [p].[Sku] = @key
ORDER BY [p].[Sku]
```

## Conclusions

NOT MEASURED. Write these from your results, including any index that did not earn its write and storage cost.
