# ApexPost Logistics Counter Platform

ApexPost is a .NET 10 / C# 14 ASP.NET Core MVC counter platform for branch retail sales, shipping, money orders, identity checks, inventory, reporting, and audit. The solution uses a layered domain/application/infrastructure/web architecture. Persistence is asynchronous JSON behind repository interfaces; there is no database or ORM.

## Requirements and run

1. Install the .NET 10 SDK.
2. From the solution directory, set local-only demo credentials:

```powershell
dotnet user-secrets set "Authentication:DemoPassword" "Choose-a-local-demo-password" --project ApexPost.Web
dotnet user-secrets set "Integration:ApiKey" "Choose-a-long-random-partner-key" --project ApexPost.Web
```

3. Restore, build, test, and run:

```powershell
dotnet restore
dotnet build ApexPost.slnx
dotnet test ApexPost.Test/ApexPost.Test.csproj
dotnet run --project ApexPost.Web
```

Open the HTTPS URL printed by `dotnet run`. The demo sign-in uses one locally configured password for seeded staff accounts. A login is intentionally unavailable until `Authentication:DemoPassword` is configured.

### Web configuration reference

All settings read by `ApexPost.Web` are declared in `ApexPost.Web/appsettings.json`. Secret-valued keys are present as empty strings only to show the configuration shape; they are intentionally unusable until supplied through user-secrets or deployment secrets. Do not replace these placeholders with real credentials in the checked-in file.

| Key | Checked-in default | Purpose |
|---|---|---|
| `Authentication:DemoPassword` | Empty | Shared local demo login password; required to sign in. |
| `Integration:ApiKey` | Empty | Optional legacy single-partner API key. |
| `Integration:Partners:<PartnerId>:ApiKey` | `CourierA` shown with empty key | Per-partner API key; add one entry per integration partner. |
| `ApiKeys:Courier` | Empty | Legacy alias for a single courier API key. |
| `Security:AllowedIps` | Loopback addresses | Client IP allowlist for integration and administrator routes. |
| `ForwardedHeaders:KnownProxies` | Empty | Explicit trusted reverse-proxy addresses; configure when deployed behind a proxy. |
| `MaintenanceMode:Enabled` | `false` | Enables the maintenance response while leaving `/health` available. |
| `Cors:PartnerTrackingWidgetOrigin` | Example HTTPS origin | Browser origin permitted for the partner tracking widget. |
| `Transactions:HighValueMoneyOrderThreshold` | `1000` | Identity-verification threshold in AUD. |
| `Transactions:MaximumMoneyOrderAmount` | `20000` | Maximum money-order principal in AUD. |
| `ShippingPricing:ParcelWeightSurchargePerKilogram` | `1.5` | Default parcel weight surcharge in AUD/kg. |
| `CourierTracking:TimeoutMs` | `1500` | Simulated tracking lookup timeout. |
| `CourierTracking:SimulatedDelayMs` | `50` | Simulated tracking lookup delay. |
| `Performance:SlowActionThresholdMs` | `500` | Action filter warning threshold. |
| `Performance:SlowRequestThresholdMs` | `500` | Request timing warning threshold. |

The login and partner request quotas are intentionally code-defined policies (3 login attempts/minute per IP and 60 requests/minute per partner), not configuration values.

## Demo staff accounts

Use the configured demo password with these seeded identities:

| Role | Email | Staff ID |
|---|---|---|
| Branch Manager | `alice@apexpost.com` | STF001 |
| Counter Clerk | `ben@apexpost.com` | STF002 |
| Auditor | `frank@apexpost.com` | STF006 |
| Administrator | `olive@apexpost.com` | STF015 |

Seeded staff and branches are in `ApexPost.Infrastructure/Data/staff.json` and `branch.json`. The role-specific audit and staff pages enforce authorization policies.

## Features

- Branch terminals, branch operating hours, staff roles, and administrator staff add/edit/deactivate. Staff uniqueness/branch rules and ID allocation live in an application service with process-local write serialization.
- A branch-scoped product catalogue and inventory view; stock is updated atomically per JSON repository and cannot go negative.
- A multi-line retail/shipping/money-order/identity-verification counter basket with payment, parcel validation, shipping pricing, and an identity threshold rule. Checkout reserves stock, persists an open transaction marker, and compensates reservations if completion fails.
- Identity verification uses a replaceable simulated format-check service; the clerk cannot mark a check successful. The interface retains only a masked document value and result, and the UI explains that no external identity provider is contacted.
- Branch-manager stock adjustments require a quantity change and enumerated reason, enforce branch ownership and non-negative stock, and are included in the append-only audit log.
- Voids and refunds restock retail items with per-stock-row idempotency markers so a partially completed reversal can resume without duplicating completed rows.
- Shipping label preview, including ten data-backed destination zones and service levels.
- Reports for daily retail/services sales, low stock, shipping volumes, and an auditor-only money-order/identity-verification summary.
- Domain events for low stock, completed transactions, money orders, and void/refund notifications.
- Append-only JSON Lines audit storage and `AUDIT-SAMPLE.json`. Full document numbers, passwords, API keys, and card data are not written to audit entries.
- Configurable shipping surcharge (`ShippingPricing:ParcelWeightSurchargePerKilogram`) and injectable shipping-pricing delegate; transaction status/payment state is verified to survive JSON reloads.
- Cookie authentication with staff, role, and branch claims; antiforgery on state-changing form posts.
- More than 13 active middleware stages for security, request handling, routing, compression, rate limiting, and observability. The exact order is documented in `DESIGN.md`.

## API key and network configuration

Partner API keys are read from user-secrets or deployment configuration. Configure a separate key per partner and do not place real keys in `appsettings*.json` or source control. The legacy `Integration:ApiKey` setting remains supported for a single partner:

```powershell
dotnet user-secrets set "Integration:ApiKey" "Choose-a-long-random-partner-key" --project ApexPost.Web
```

For multiple partners, use a unique secret for each configured partner ID:

```powershell
dotnet user-secrets set "Integration:Partners:CourierA:ApiKey" "Courier-A-long-random-key" --project ApexPost.Web
dotnet user-secrets set "Integration:Partners:CourierB:ApiKey" "Courier-B-long-random-key" --project ApexPost.Web
```

Example request:

```powershell
curl.exe -H "X-Api-Key: <configured-key>" https://localhost:5001/integration/parcel-status
curl.exe https://localhost:5001/health
```

The integration endpoint and administrator staff routes also apply an IP allowlist; the integration endpoint applies a per-partner rate limit. For production behind a reverse proxy, configure `ForwardedHeaders:KnownProxies` with the proxy IP addresses, then configure `Security:AllowedIps` for the actual approved clients. Forwarded headers are accepted only from trusted proxies; trusting arbitrary `X-Forwarded-For` values lets a caller spoof its address. Set `Cors:PartnerTrackingWidgetOrigin` to the approved widget origin. CORS is a browser policy, not authentication or an API security boundary.

The simulated courier tracking lookup has a bounded timeout. Configure `CourierTracking:TimeoutMs` and `CourierTracking:SimulatedDelayMs` to exercise success and timeout behavior; a lookup timeout returns 504, while caller cancellation remains cancellation.

Shipping rates use configured zone multipliers and a tiered service-level base rate. The parcel weight surcharge defaults to AUD 1.50/kg and can be overridden with `ShippingPricing:ParcelWeightSurchargePerKilogram`; negative values fail startup validation.

## Audit sample provenance

`AUDIT-SAMPLE.json` is generated from actual JSON Lines audit records, not hand-authored examples. Run the app and perform audited counter or stock actions, then export only those events and a strict allowlist of safe audit fields:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-AuditSample.ps1
```

The default input is `ApexPost.Web\Data\audit-log.jsonl`. The checked-in sample includes a successfully completed and persisted sale (HTTP 302 PRG result) as well as a stock adjustment and representative counter requests; rerun the exporter after creating new actions to refresh it. The exporter fails clearly if the file or eligible audit entries are missing. It does not export request bodies or free-form form values.

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

The included seed set contains five branches, twenty staff, thirty products, 150 branch/product stock records, ten shipping-zone rates, and forty paid historical transactions. JSON files live in `ApexPost.Infrastructure/Data` and are copied to the application output. Branch opening hours use the branch's configured timezone. Grant the running process write access to its output `Data` directory for mutable JSON and audit files. Repository locks and staff-write serialization are process-local: they prevent concurrent writes within one running instance, but do not provide cross-process coordination or multi-file crash atomicity. Run one instance for this JSON-persistence assignment; replace it with transactional storage before scaling writers.

## Solution layout

- `ApexPost.Domain`: entities, rules, events, enums, value objects.
- `ApexPost.Application`: repository contracts, use cases, reports, pricing abstractions.
- `ApexPost.Infrastructure`: asynchronous JSON stores, simulated courier service, cache, notifications, event dispatch.
- `ApexPost.Web`: MVC controllers/views, authentication, middleware, filters, composition root.
- `ApexPost.Test`: domain unit tests and WebApplicationFactory integration tests.

See `DESIGN.md` for the architecture/domain diagrams, middleware sequence, filters, ADRs, security decisions, and C# 14 feature rationale.
