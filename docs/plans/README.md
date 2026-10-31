# Execution plans

Actual execution plans captured from SQL Server 2022, as described in `PERFORMANCE.md`. Open any `.sqlplan` file in SSMS.

| Prefix | Contents |
|---|---|
| `W1`–`W4` | The four workloads, before and after their indexes |
| `P1`–`P3` | Stored procedures |
| `S1` | The non-SARGable predicate and its fix |
| `M1`, `M2` | Estimated versus actual rows, with Parameter Sensitive Plan optimization on and off |
| `M3` | W2's estimate of 1 reproduced from a sampled statistic, and the same query after `FULLSCAN` |
