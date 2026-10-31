# ApexPost Logistics Counter Platform

ApexPost is a .NET 10 / C# 14 ASP.NET Core MVC counter platform for branch retail sales, shipping, money orders, identity checks, inventory, reporting, and audit. The solution uses a layered domain/application/infrastructure/web architecture. Persistence is asynchronous JSON behind repository interfaces; there is no database or ORM.

## Requirements and run

1. Install the .NET 10 SDK.
2. From the solution directory, set a partner API key (needed only for `/integration/parcel-status`):

```bash
dotnet user-secrets set "Integration:ApiKey" "Choose-a-long-random-partner-key" --project ApexPost.Web
```

3. Build, test and run:

```bash
dotnet build ApexPost.slnx
```

```bash
dotnet test ApexPost.slnx
```

```bash
dotnet run --project ApexPost.Web --launch-profile https
```

Open the HTTPS URL it prints and sign in with an account below. Warnings fail the build through `Directory.Build.props`.

Runtime data is written to `ApexPost.Web/App_Data` (ignored by Git) and survives clean and rebuild. To reset the demo, stop the app and delete that folder; the seed data then loads again.

### Web configuration reference

Most settings read by `ApexPost.Web` are declared in `ApexPost.Web/appsettings.json`. Secret-valued keys are present as empty strings only to show the configuration shape; they are intentionally unusable until supplied through user-secrets or deployment secrets. Do not replace these placeholders with real credentials in the checked-in file.

| Key | Checked-in default | Purpose |
|---|---|---|
| `Persistence:DataDirectory` | `App_Data` | Single folder for runtime JSON and the audit log, absolute or relative to the content root. |
| `Inventory:LowStockThreshold` | `5` | Quantity at or below which a product counts as low stock. |
| `Integration:ApiKey` | Empty | Single-partner API key, reported as partner `default`. |
| `Integration:Partners:<PartnerId>:ApiKey` | `CourierA` shown with empty key | Per-partner API key; add one entry per integration partner. |
| `Security:AllowedIps` | Loopback addresses | Client IP allowlist for integration and administrator routes. |
| `ForwardedHeaders:KnownProxies` | Empty | Explicit trusted reverse-proxy addresses; configure when deployed behind a proxy. |
| `MaintenanceMode:Enabled` | `false` | Enables the maintenance response while leaving `/health` available. |
| `MaintenanceMode:RetryAfterSeconds` | `120` | `Retry-After` value during maintenance. |
| `RateLimiting:Login` | 3 per 60 s | `PermitLimit` and `WindowSeconds` per client IP. |
| `RateLimiting:Partner` | 60 per 60 s | `PermitLimit` and `WindowSeconds` per partner. |
| `CourierRates:MaxConcurrentRequests` | `4` | Concurrent calls allowed to the simulated courier rate service. |
| `CourierRates:SimulatedDelayMs` | `20` | Simulated courier rate latency. |
| `Transactions:MoneyOrderFee` | `10` | Fee per money order in AUD. |
| `Cors:PartnerTrackingWidgetOrigin` | Example HTTPS origin | Browser origin permitted for the partner tracking widget. |
| `Transactions:HighValueMoneyOrderThreshold` | `1000` | Identity-verification threshold in AUD. |
| `Transactions:MaximumMoneyOrderAmount` | `20000` | Maximum money-order principal in AUD. |
| `ShippingPricing:ParcelWeightSurchargePerKilogram` | `1.5` | Default parcel weight surcharge in AUD/kg. |
| `CourierTracking:TimeoutMs` | `1500` | Simulated tracking lookup timeout. |
| `CourierTracking:SimulatedDelayMs` | `50` | Simulated tracking lookup delay. |
| `Performance:SlowActionThresholdMs` | `500` | Action filter warning threshold. |
| `Performance:SlowRequestThresholdMs` | `500` | Request timing warning threshold. |

Settings are validated at startup, so an invalid value stops the app with a clear error.

## Demo staff accounts

Every staff member has their own password, stored only as a salted PBKDF2-SHA256 hash in `staff.json`. These are fictional seed accounts for local demos and must not be used in production.

| Role | Email | Password | Staff ID | Branch |
|---|---|---|---|---|
| Branch Manager | `alice@apexpost.com` | `Apex-Alice-2026!` | STF001 | B001 |
| Counter Clerk | `ben@apexpost.com` | `Apex-Ben-2026!` | STF002 | B001 |
| Auditor | `frank@apexpost.com` | `Apex-Frank-2026!` | STF006 | B002 |
| Administrator | `olive@apexpost.com` | `Apex-Olive-2026!` | STF015 | B001 |

All 20 seeded accounts follow the same pattern, `Apex-<Name>-2026!`, using the capitalised name from the email address. Administrators can set a new password on the staff edit screen. Login allows 3 attempts per minute per IP address, so wait a minute after that.

Branches enforce their real opening hours in their own time zone, so a sale outside those hours is rejected. An administrator can change the hours on the Branches screen to demonstrate sales at any time.

## Features

- Administrator screens to add and edit branches (hours, time zone, terminals), close or reopen them, and add and edit products. Stock rows and the catalogue cache stay in step automatically.
- A transactions screen where Branch Managers void or refund a paid sale, with a required reason.
- Branch terminals, branch operating hours, staff roles, and administrator staff add/edit/deactivate with per-staff passwords. Staff uniqueness/branch rules and ID allocation live in an application service with process-local write serialization.
- A branch-scoped product catalogue and inventory view; stock is updated atomically per JSON repository and cannot go negative.
- A counter basket mixing retail lines, several shipments and several money orders with an identity check and payment, plus dedicated money order and identity verification screens. Parcel validation, shipping pricing and the identity threshold rule all apply. Checkout reserves stock, persists an open transaction marker, and compensates reservations if completion fails.
- Identity verification uses a replaceable simulated format-check service; the clerk cannot mark a check successful. The interface retains only a masked document value and result, and the UI explains that no external identity provider is contacted.
- Branch-manager stock adjustments require a quantity change and enumerated reason, enforce branch ownership and non-negative stock, and are included in the append-only audit log.
- Voids and refunds restock retail items with per-stock-row idempotency markers so a partially completed reversal can resume without duplicating completed rows.
- Shipping label preview with the recipient address, ten data-backed destination zones and service levels. Parcels need dimensions and have service-level weight limits; letters are limited to 0.5 kg.
- Reports with a date picker for daily retail/services sales (opening on the latest day with sales), low stock, shipping volumes, and an auditor-only money-order/identity-verification summary.
- Domain events for low stock, completed transactions, money orders, and void/refund notifications, shown on that branch's dashboard.
- Append-only JSON Lines audit storage of business actions only (sales, money orders, identity checks, voids, refunds, stock, and staff, branch and product changes), including denied attempts, and `AUDIT-SAMPLE.json`. A failed audit write never fails the business action. Full document numbers, passwords, API keys, and card data are not written to audit entries.
- Configurable shipping surcharge (`ShippingPricing:ParcelWeightSurchargePerKilogram`) and injectable shipping-pricing delegate; transaction status/payment state is verified to survive JSON reloads.
- Cookie authentication with staff, role, and branch claims; antiforgery on state-changing form posts.
- More than 13 active middleware stages for security, request handling, routing, compression, rate limiting, and observability. The exact order is documented in `DESIGN.md`.

## Screenshots and sample requests

Screens captured from the running app are in [`docs/screenshots`](docs/screenshots):

| Screen | File |
|---|---|
| Login | [01-login.png](docs/screenshots/01-login.png) |
| Branch dashboard | [02-branch-dashboard.png](docs/screenshots/02-branch-dashboard.png) |
| Catalogue and stock | [03-catalogue-and-stock.png](docs/screenshots/03-catalogue-and-stock.png) |
| Counter transaction | [04-counter-transaction.png](docs/screenshots/04-counter-transaction.png) |
| Money order | [05-money-order.png](docs/screenshots/05-money-order.png) |
| Identity verification | [06-identity-verification.png](docs/screenshots/06-identity-verification.png) |
| Shipping label | [07-shipping-label.png](docs/screenshots/07-shipping-label.png) |
| Transactions, void and refund | [08-transactions-void-refund.png](docs/screenshots/08-transactions-void-refund.png) |
| Reports | [09-reports.png](docs/screenshots/09-reports.png) |
| Auditor report | [10-auditor-report.png](docs/screenshots/10-auditor-report.png) |
| Staff, branch and product management | [11](docs/screenshots/11-staff-management.png), [12](docs/screenshots/12-branch-management.png), [13](docs/screenshots/13-product-management.png) |

Import [`docs/ApexPost.postman_collection.json`](docs/ApexPost.postman_collection.json) into Postman for sample requests.

## API key and network configuration

Partner API keys are read from user-secrets or deployment configuration. Configure a separate key per partner and do not place real keys in `appsettings*.json` or source control. `Integration:ApiKey` is the simplest option for a single partner:

```bash
dotnet user-secrets set "Integration:ApiKey" "Choose-a-long-random-partner-key" --project ApexPost.Web
```

For multiple partners, use a unique secret for each configured partner ID:

```bash
dotnet user-secrets set "Integration:Partners:CourierA:ApiKey" "Courier-A-long-random-key" --project ApexPost.Web
```

```bash
dotnet user-secrets set "Integration:Partners:CourierB:ApiKey" "Courier-B-long-random-key" --project ApexPost.Web
```

Example request:

```bash
curl -k -H "X-Api-Key: <configured-key>" https://localhost:7125/integration/parcel-status
```

```bash
curl -k https://localhost:7125/health
```

The integration endpoint and administrator staff routes also apply an IP allowlist; the integration endpoint applies a per-partner rate limit. For production behind a reverse proxy, configure `ForwardedHeaders:KnownProxies` with the proxy IP addresses, then configure `Security:AllowedIps` for the actual approved clients. Forwarded headers are accepted only from trusted proxies; trusting arbitrary `X-Forwarded-For` values lets a caller spoof its address. Set `Cors:PartnerTrackingWidgetOrigin` to the approved widget origin. CORS is a browser policy, not authentication or an API security boundary.

The simulated courier tracking lookup has a bounded timeout. Configure `CourierTracking:TimeoutMs` and `CourierTracking:SimulatedDelayMs` to exercise success and timeout behavior; a lookup timeout returns 504, while caller cancellation remains cancellation.

Shipping rates use configured zone multipliers and a tiered service-level base rate. The parcel weight surcharge defaults to AUD 1.50/kg and can be overridden with `ShippingPricing:ParcelWeightSurchargePerKilogram`; negative values fail startup validation.

## Audit sample provenance

`AUDIT-SAMPLE.json` holds real entries produced by the running app (a mixed sale, money order, identity check, stock adjustment, void, refund, staff, branch and product changes, and denied attempts), passed through the safe-field allowlist. To refresh it on Windows, run the app, perform audited actions, then:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-AuditSample.ps1
```

The default input is `ApexPost.Web\App_Data\audit-log.jsonl`. The exporter does not copy request bodies or free-form values.

## Git workflow

Use a short-lived feature branch for each coherent assignment change, push it, and open a pull request with a scope-specific title. Require a warning-free build and passing tests before merge, and never include user-secrets or runtime audit logs. The repository history contains merge commits for PRs `#1` and `#2`; continue the same reviewable workflow for later changes.

Example local sequence:

```powershell
git switch -c feature/audit-sample-provenance
git add README.md DESIGN.md tools/Export-AuditSample.ps1
git commit -m "docs: make audit evidence reproducible"
git push -u origin feature/audit-sample-provenance
```

## Seed data

The included seed set contains five branches, twenty staff, thirty products, 150 branch/product stock records, ten shipping-zone rates, and forty paid historical transactions. Seed JSON files live in `ApexPost.Infrastructure/Data` and are copied read-only to the application output. Branch opening hours use the branch's configured timezone. Grant the running process write access to the data folder (`App_Data` by default). Repository locks and staff-write serialization are process-local: they prevent concurrent writes within one running instance, but do not provide cross-process coordination or multi-file crash atomicity. Run one instance for this JSON-persistence assignment; replace it with transactional storage before scaling writers.

## Solution layout

- `ApexPost.Domain`: entities, rules, events, enums, value objects.
- `ApexPost.Application`: repository contracts, use cases, reports, pricing abstractions.
- `ApexPost.Infrastructure`: asynchronous JSON stores, simulated courier service, cache, notifications, event dispatch.
- `ApexPost.Web`: MVC controllers/views, authentication, middleware, filters, composition root.
- `ApexPost.Test`: 95 domain unit tests and WebApplicationFactory integration tests, run against a temporary data folder.

See `DESIGN.md` for the architecture/domain diagrams, middleware sequence, filters, ADRs, security decisions, and C# 14 feature rationale.
