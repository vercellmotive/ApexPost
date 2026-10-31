# Git history

Repository: <https://github.com/vercellmotive/ApexPost>. A zip leaves out `.git`, so this snapshot shows how the work was done, up to PR #12. It can't list the commit that updates it, so GitHub is the full record.

## Pull requests

| PR | Title | State |
|---|---|---|
| [#1](https://github.com/vercellmotive/ApexPost/pull/1) | Address Week 2 assessment remarks | MERGED |
| [#2](https://github.com/vercellmotive/ApexPost/pull/2) | Close remaining Week 2 assessment gaps | MERGED |
| [#3](https://github.com/vercellmotive/ApexPost/pull/3) | Move persistence to SQL Server with EF Core 10 | MERGED |
| [#4](https://github.com/vercellmotive/ApexPost/pull/4) | Add SQL Server evidence and replace the low-stock index | MERGED |
| [#5](https://github.com/vercellmotive/ApexPost/pull/5) | Close the remaining Week 3 assessment gaps | MERGED |
| [#6](https://github.com/vercellmotive/ApexPost/pull/6) | Close the final Week 3 assessment gaps | MERGED |
| [#7](https://github.com/vercellmotive/ApexPost/pull/7) | Close the gaps from the 91/100 review | MERGED |
| [#8](https://github.com/vercellmotive/ApexPost/pull/8) | Explain the W2 estimate mismatch | MERGED |
| [#9](https://github.com/vercellmotive/ApexPost/pull/9) | Close three small gaps from the wider checks | MERGED |
| [#10](https://github.com/vercellmotive/ApexPost/pull/10) | Close the gaps from grading against the brief | MERGED |
| [#11](https://github.com/vercellmotive/ApexPost/pull/11) | Return Problem Details from action errors | MERGED |
| [#12](https://github.com/vercellmotive/ApexPost/pull/12) | Make the README setup work from a clean unzip | MERGED |

## Commits on `master` (merges left out)

| Commit | Date | Message |
|---|---|---|
| `07f39c7` | 2026-10-23 | Add .gitattributes, .gitignore, and README.md. |
| `4c5b571` | 2026-10-23 | Add project files. |
| `8574cdc` | 2026-10-23 | feat(domain): core domain models, business rules1-6, and xunit tests |
| `3065b8e` | 2026-10-25 | Final submittion commit. |
| `3d55179` | 2026-10-31 | fix: address Week 2 assessment remarks |
| `bc2f5dc` | 2026-10-31 | fix: close remaining Week 2 assessment gaps |
| `645175c` | 2026-10-31 | feat: move persistence to SQL Server with EF Core 10 |
| `5481b0e` | 2026-10-31 | feat: capture SQL Server evidence and replace low-stock index |
| `9b88289` | 2026-10-31 | refactor: replace ViewBag in staff and service forms |
| `e9f9868` | 2026-10-31 | feat: store shipment destination address as an owned type |
| `c2438ed` | 2026-10-31 | feat: retry sales that lose a rowversion race |
| `17164fa` | 2026-10-31 | fix: stop logging expected failures as EF Core errors |
| `ed96297` | 2026-10-31 | test: prove the least-privilege role on SQL Server |
| `3f45ba1` | 2026-10-31 | docs: describe the SQL Server system and refresh evidence |
| `b8b37b1` | 2026-10-31 | refactor: move line item and manager rules into the Domain |
| `9d9d171` | 2026-10-31 | test: force a real deadlock on SQL Server |
| `568e21e` | 2026-10-31 | test: add a load test script and its results |
| `7f900f9` | 2026-10-31 | docs: record load and deadlock evidence and Git history |
| `96f84eb` | 2026-10-31 | fix: restock voids and refunds through the procedure in one transaction |
| `92af4e2` | 2026-10-31 | docs: add ER columns, raw statistics output and consistent test counts |
| `ea1106d` | 2026-10-31 | docs: explain the W2 estimate mismatch with measured evidence |
| `4559425` | 2026-10-31 | style: apply dotnet format whitespace fixes |
| `03b908c` | 2026-10-31 | test: cover the high-value money order boundary at checkout |
| `44ddec5` | 2026-10-31 | docs: bring the Git history snapshot up to PR #8 |
| `62bd687` | 2026-10-31 | fix: record the real user on stock history and test reports on SQL Server |
| `030dc08` | 2026-10-31 | fix: make HAVING do real work in the reporting queries |
| `35d2e5a` | 2026-10-31 | test: restore the Week 2 rules file and name tests by behaviour |
| `dbfe414` | 2026-10-31 | docs: tidy ADRs, record deviations and update counts |
| `bdf981c` | 2026-10-31 | fix: return Problem Details from void, refund, stock and admin errors |
| `bafa304` | 2026-10-31 | docs: record the error boundary decision and update counts |
| `c439882` | 2026-10-31 | docs: make the README setup work from a clean unzip |

## Early commit messages

The first commits have weak messages, such as `3065b8e` ("Final submittion commit."), which holds 84 files. They stay as they are: rewriting them would change every later commit hash on `master`, and break the links from the pull requests above. Work since then uses a feature branch, a pull request and a message that says what changed.
