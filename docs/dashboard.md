# Landlord dashboard

The canonical landlord route is `/app/dashboard`; `/landlord` remains an alias
displaying the same dashboard. Login and permission-aware navigation use the
canonical route. Tenant, contractor and administration frames remain layout-only.

## API and authorization

`GET /api/v1/dashboard` (compatible `/api/dashboard`) requires a real cookie
session and an active owner/property-manager membership. Select the organization
with `X-Organization-Id`. Owners see their organization's non-archived properties;
managers see only their current property assignments. Super-administrator status
does not bypass this membership requirement.

Optional `startDate`, `endDate` and `propertyId` query parameters must each occur
once; unknown parameters are rejected. Dates use ISO `yyyy-MM-dd`, are inclusive,
and default to the current UTC month. A reporting request spans at most five calendar years
to bound monthly aggregation work, as selected for this stage. The inclusive end
must precede start + five calendar years (leap-day anniversaries clamp to February
28). Invalid input returns validation Problem
Details (400). A foreign, unassigned, missing or archived property ID returns the
same 404; changing organization without membership returns 403.

The Application Reporting contract is implemented by an Infrastructure read
service. The endpoint never reads EF entities directly. Every source is filtered
by the same authorized property query, including expenses and activity. EF
organization query filters remain active; PostgreSQL requests set the existing
transaction-local RLS context. All counts, amounts and lists are read in one
consistent snapshot (Repeatable Read on PostgreSQL, Serializable on SQLite).
No tables, migrations, recurring-charge ledger or production seed data are added.

## Metric definitions

| Metric | Definition |
|---|---|
| Properties | Current non-archived authorized properties, including inactive ones |
| Units | Current non-archived units under those properties |
| Occupied | Current `Occupied` units |
| Vacant | Current `Available` units; `Unavailable` is displayed separately |
| Occupancy | Occupied / all non-archived units, including unavailable; percentage rounded to two decimals; null when no units |
| Rental income | `Completed` payments whose `PaidAt` falls within the inclusive UTC reporting dates |
| Outstanding rent | Recorded `Pending` or `Failed` payments due on/before reporting end, including older arrears; no synthesized recurring charges |
| Expenses | `Approved` or `Paid` expenses with `IncurredDate` inside the reporting dates |
| Pending maintenance | Current `Open`, `Assigned` or `InProgress` requests; not date-filtered |
| Expiring leases | Currently `Active` leases ending strictly after reporting end and on/before end + 30 days |

Cancelled/refunded payments and draft/cancelled expenses are excluded. Monetary
totals and charts are grouped by currency without conversion or a cross-currency
sum. A multi-month filter changes the financial cards to “in period”; charts show
calendar-month buckets clipped to the inclusive filter. Months without records
are returned as empty buckets and rendered as zero bars for known currencies.
No currency or nonzero statistics are fabricated for an empty portfolio.

Financial records remain accessible through archived units under a non-archived
property, preserving valid historical transactions. Archived properties are
excluded consistently from all dashboard surfaces. No historical inventory or
status reconstruction is claimed: entities do not retain that history.

Aggregation runs in SQL, including decimal sums with the existing SQLite
minor-unit conversion. Only grouped amounts, property choices, and bounded lists
are materialized. Requests perform sequential queries because EF contexts are not
thread-safe. There is no cache or background synchronization; Apply/Retry reload
the snapshot. The normal shared client timeout and cancellation still apply.

## Activities, alerts and shortcuts

Activity is the latest ten genuine lease/payment/expense/maintenance records
whose `UpdatedAt` is in the reporting interval. Each source contributes at most
ten candidates, merged in timestamp/kind/ID order. Newly created entities start
with an update timestamp. This is a latest-record view, **not an event log or full
audit trail**. Updates use the current record label and status.

Alerts are derived from recorded outstanding amounts, current pending jobs, and
the lease-expiry count. The earliest ten expiring leases are displayed, with the
full matching count. No tenant personal details are included.

Shortcuts navigate to finances, alerts, lease expiries and activity. Add-property
and create-lease buttons are explicitly disabled “Coming soon”; no business
write endpoints or screens are implemented.

## Frontend behavior and accessibility

The shared cookie/Problem Details client handles all communication. Runtime guards
check scope, requested filters, count consistency, finite money, currencies and
exact month buckets. Organization changes remount the dashboard, resetting
property/date selections and aborting requests. Applied filters/retries hide old
results immediately; aborted late responses cannot replace current content.

Draft filters do not change displayed statistics until applied. The route parent
retains only filter preferences (not records) during fail-closed session
revalidation, so browser focus does not reset inputs or applied dates. Changing
organization resets these preferences. Properties remain
selectable from the last successful response for the same organization, allowing
recovery from a removed assignment. Loading, errors, validation/focus and retry
are explicit. Anonymous/unauthorized users never mount the dashboard.

Charts use CSS bars rather than another dependency. Each financial chart has an
accessible currency caption and expandable table with exact values. Occupancy has
a readable count list; decorative bars are hidden from assistive technology.
The existing responsive landlord sidebar, top bar, organization selector, account
menu, notifications placeholder and breadcrumbs are reused.

## Files

Created:
- `backend/Demeure.Application/Modules/Reporting/Contracts/DashboardContracts.cs`
- `backend/Demeure.Infrastructure/Authorization/DashboardReader.cs`
- `backend/Demeure.Api/Endpoints/DashboardEndpoints.cs`
- `backend/Demeure.Tests/Integration/DashboardTests.cs`
- `frontend/src/lib/dashboard-api.ts` and `dashboard-api.test.ts`
- `frontend/src/pages/dashboard.tsx` and `dashboard.test.tsx`
- `docs/dashboard.md`

Modified:
- `backend/Demeure.Infrastructure/Authentication/DependencyInjection.cs`
- `backend/Demeure.Api/Extensions/ApiConventions.cs`
- `backend/Demeure.Api/Extensions/OpenApiConventions.cs`
- `backend/Demeure.Api/Demeure.Api.http`
- `frontend/src/app-router.tsx`
- `frontend/src/pages/application-layouts.tsx`
- `frontend/src/components/layout/permissions.ts` and `layouts.test.tsx`
- `frontend/src/lib/login-navigation.ts`
- `README.md`, `docs/layouts.md`, `docs/frontend-integration.md`, `docs/api.md`

## Verification and limitations

Dashboard integration tests use real Identity sessions and isolated SQLite
databases, checking exact cents/currencies, UTC boundaries, refund/cancellation,
date/property scope, manager assignments, cross-organization tampering, role
denial, empty states, aliases and month buckets. Frontend tests validate transport,
runtime DTO guards, queries, canonical login navigation and accessible charts.

Verified for this stage:
- Backend: 297 passed, one pre-existing live PostgreSQL test skipped.
- Frontend: 194 passed; lint and production build clean.
- Live isolated API: readiness 200, anonymous dashboard 401, and OpenAPI exposes
  date/property/organization parameters and the expected response codes.
- Test-only browser fixtures: populated/empty/error/retry states, date/property
  filters, validation focus, preference retention on session refresh and reset on
  organization changes. A late old-organization response completed after the
  new response and did not restore old content.
- A five-year report rendered 60 monthly table rows. At 320px, long selected
  property names and expanded chart tables remain inside the page; chart/table
  scrolling is local. Layout checks also covered 375, 768, 1024 and 1440px.

Browser fixtures are isolated verification data, never shipped application data.
The metric/access tests themselves use real API authentication and persistence.
Temporary servers and the isolated smoke database are removed after validation.

Live PostgreSQL validation still requires an available isolated PostgreSQL
database and non-superuser runtime role. Existing RLS is not changed by this
feature. No charge ledger means outstanding rent is only as complete as the
recorded payment data. Creating/managing those records is a subsequent stage.
