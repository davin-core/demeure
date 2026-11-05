# Demeure

A multi-tenant apartment rental and property management SaaS foundation built
with ASP.NET Core 10 and React + TypeScript + Vite. This foundation contains
architecture, EF Core Code First persistence, Identity authentication, operational health checks, and a
developer connectivity screen and interactive frontend design system. PostgreSQL is the production target; SQLite is
available locally. Protected read-only resource endpoints enforce organization,
property-assignment and self-service scopes. The frontend has real cookie login
and shared typed API clients, plus a live landlord dashboard at `/app/dashboard`.
Business CRUD, registration/recovery screens and
production seed data are not implemented.

## Structure

```text
Demeure.slnx
global.json
backend/
  Demeure.Api/              HTTP endpoints, API DTOs, configuration, DI composition root
  Demeure.Application/      Application contracts and organization-context abstraction
  Demeure.Domain/           Organization-scoped entities and statuses
  Demeure.Infrastructure/   EF model, PostgreSQL/SQLite providers and migrations
  Demeure.Tests/            xUnit unit, API and SQLite relational integration tests
docs/
  architecture.md          Module boundaries and architectural conventions
  database.md              Schema, migrations, provider differences and Mermaid ER diagram
  domain.md                Entity construction, validation and lifecycle rules
  authentication.md        Cookie/CSRF flows, organization roles and email delivery
  authorization.md         Resource permissions, audited platform access and PostgreSQL RLS
  api.md                   Versioning, collection queries, responses and local Swagger UI
  design-system.md         Visual tokens, reusable components and accessibility conventions
  layouts.md               Public and permission-aware portal layouts, session and menus
  frontend-integration.md  Shared clients, login, pagination and deployment conventions
  dashboard.md             Dashboard metric definitions, reporting scope and verification
frontend/
  src/
    components/ui/         Locally owned shadcn/ui components
    components/layout/     Shared brand, session, menus and responsive application layouts
    pages/                 Login, live landlord dashboard, design gallery and portal layouts
    lib/                   API client and shared UI utilities
    App.tsx                Developer connectivity screen
  components.json          shadcn/ui configuration
  .env.example             Public API path and server-only proxy target
```

## Prerequisites

- .NET 10 SDK. `global.json` allows stable .NET 10 feature bands.
- Node.js 22.12 or newer; use a supported LTS release.
- npm (included with Node.js).

## Run locally

Run these commands from the repository root in separate PowerShell terminals.

Start the API:

```powershell
dotnet restore Demeure.slnx
npm ci --prefix backend\Demeure.Api
dotnet run --project backend\Demeure.Api
```

The development launch profile uses `http://localhost:5076` and sets
`ASPNETCORE_ENVIRONMENT=Development`.
Development uses SQLite without a server. To initialize the local database, see
[SQLite setup](docs/database.md#local-sqlite-setup). Schema changes are applied
explicitly with EF migrations, not automatically at API startup.

Start the frontend:

```powershell
Set-Location frontend
Copy-Item .env.example .env.local
npm ci
npm run dev
```

Copy the environment template only on first setup; do not overwrite an existing
local configuration. Open `http://localhost:5173`. The page checks the actual API,
supports refresh, and displays network, timeout, HTTP, and response-validation errors.
Vite forwards `/api` requests to `http://localhost:5076`, avoiding cross-origin
requests in local development.

Open `http://localhost:5173/design-system` for the Demeure component gallery:
colors, typography, spacing, buttons, forms, cards, tables, dialogs and feedback.
Examples are explicitly local-only; no accounts or property records are created.
The original API workspace remains at `/`. See the
[design-system guide](docs/design-system.md) for usage, accessibility, the file
inventory and production SPA fallback configuration.

Application layouts are available at `/public`, `/app/dashboard` (alias `/landlord`), `/tenant`,
`/contractor` and `/administration`. Public content is anonymous; portals use the
real cookie session and organization/global roles. Without a session, portals
show a sign-in-required state with a link to `/login`.
The landlord dashboard uses real scoped backend metrics, date/property filters,
charts, record activity and derived alerts. Other permitted business destinations
are disabled and labeled Coming soon. See the [dashboard guide](docs/dashboard.md)
for metric definitions and architectural decisions, and the
[layout guide](docs/layouts.md) for access, organization switching, menus and
the file inventory.

Sign in at `/login` using a verified account created through the existing API.
Login uses cookies and fresh CSRF tokens; successful navigation is limited to
authorized layouts. Shared resource clients and pagination are ready for future
business pages. See [frontend integration](docs/frontend-integration.md) for
contracts, usage, production configuration and the complete file inventory.

Both servers use explicit ports. If port 5173 is occupied, Vite fails rather than
silently choosing a different port. To use another frontend port, run
`npm run dev -- --port 5184`.

### Environment configuration

| Setting | Default | Purpose |
| --- | --- | --- |
| `VITE_API_BASE_URL` | `/api` | Public browser API prefix; Vite substitutes it at build time. |
| `API_PROXY_TARGET` | `http://localhost:5076` | Vite development/preview proxy destination; server-only. |
| `ASPNETCORE_ENVIRONMENT` | `Development` in the launch profile | Controls development-only OpenAPI. |
| `ASPNETCORE_URLS` | `http://localhost:5076` in the launch profile | Backend listening address. |
| `AllowedHosts` | `localhost;127.0.0.1` | Allowed backend Host headers; override for deployed domains. |
| `Database__Provider` | PostgreSQL; Sqlite in Development | Selects the EF provider explicitly. |
| `ConnectionStrings__Demeure` | Local SQLite path in Development; required in Production | Runtime database connection; real credentials must stay outside source control. |
| `AccountEmail__Delivery` | Pickup in Development; Smtp otherwise | Real local `.eml` messages or SMTP delivery. |
| `AccountEmail__PickupDirectory` | API content root/EmailMessages in Development | Ignored local verification/reset messages; protect these as secrets. |
| `AccountEmail__Host`, `AccountEmail__Port`, `AccountEmail__From` | Host required outside Development; port 587 | Production SMTP with STARTTLS. |
| `AccountEmail__UserName`, `AccountEmail__Password` | Unset | Optional SMTP credentials from secrets, never source control. |

The frontend reads `.env.local` automatically. Restart Vite after environment
changes. **Never put secrets in `VITE_*` variables**: these values are public.

ASP.NET Core reads environment variables and JSON configuration, but does not
automatically load `.env` files. `backend/.env.example` is a reference, not an
autoloaded configuration file. Double underscores represent nested keys
(for example, `Logging__LogLevel__Default`).

To override the backend port without the launch profile:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
dotnet run --project backend\Demeure.Api --no-launch-profile
```

Set `API_PROXY_TARGET=http://localhost:5080` in the frontend's `.env.local` too.
Use `dotnet user-secrets --project backend\Demeure.Api` or your deployment's secret
manager for SMTP/database credentials. Local environment files and build/test
output are ignored by Git.

### Operational endpoints

| Endpoint | Purpose |
| --- | --- |
| `GET /api/health` | Readiness: executes all registered dependency checks. |
| `GET /api/health/live` | Liveness: verifies the API can respond without dependency checks. |
| `GET /openapi/v1.json` | API specification, available only in Development. |
| `GET /swagger` | Interactive local Swagger UI, available only in Development. |

Healthy responses preserve the original contract:

```json
{ "status": "ok", "service": "demeure-api" }
```

Readiness returns HTTP 503 with `status: "unhealthy"` when a dependency fails.
Degraded checks return HTTP 200 with `status: "degraded"`; the UI does not mark
these as operational. Responses are non-cacheable and omit internal dependency details.
No external checks are registered yet, so readiness currently measures only the
foundation; it does not claim a database is connected.

Unknown routes and unhandled failures use ASP.NET Core Problem Details. Unhandled
exceptions are logged once by the centralized API exception handler; error bodies
include a trace ID but never internal exception messages. Manual requests are in
[Demeure.Api.http](backend/Demeure.Api/Demeure.Api.http).

### Authentication

Public registration creates one local account, a new organization, and an active
owner membership in a transaction. Email verification is required before login.
Development writes real verification/reset emails to the ignored
[EmailMessages directory](backend/Demeure.Api/EmailMessages); no tokens are returned
by the API or written to application logs.

Get `GET /api/auth/csrf` first, retain cookies, then send its token as
`X-CSRF-TOKEN` on **every unsafe `/api` request**, including anonymous registration
and login. Fetch a new token after login/logout. Authentication uses HttpOnly,
SameSite=Strict cookies, Secure outside Development, with a fixed eight-hour
session lifetime; JWT/refresh tokens are not used.

See [authentication setup and endpoint contracts](docs/authentication.md) for
the complete flow, SMTP configuration, lockout, role scoping and production requirements.
The frontend now implements login only. Registration, verification and recovery
remain API flows with reusable frontend clients; their screens are not implemented.

## Validation

From the repository root:

```powershell
npm ci --prefix backend\Demeure.Api
dotnet build Demeure.slnx
dotnet test backend\Demeure.Tests\Demeure.Tests.csproj

Set-Location frontend
npm run lint
npm test
npm run build
```

Backend tests cover DTO serialization, healthy/degraded/unhealthy readiness,
liveness independence, OpenAPI metadata and environment gating, and 404 Problem Details.
Architecture/convention tests also cover layer references, DI registration,
validation, error mappings, safe JSON problems and correlated request logging.
Persistence tests apply real SQLite migrations and verify composite foreign keys,
organization isolation, constraints and money/UTC round-trips. PostgreSQL model and
SQL-generation tests run without a PostgreSQL server.
Domain tests cover the eight initial entities' constructors, validation, lifecycle
matrices and atomic updates; persistence tests verify behavior after EF reload.
Authentication tests cover actual Identity/SQLite accounts, password hashing,
verification/reset, expiry, sessions/revocation, CSRF, role and organization
boundaries, lockout, rate limits, and real local email delivery.
Collection tests cover bounded paging, typed status filters, deterministic sorting,
authorized totals/items and compatible versioned routes. Documentation tests cover
OpenAPI security metadata and local Swagger UI's production gating.
Frontend tests cover API contracts, errors, configuration, cancellation, and
Tailwind class merging. Test doubles exist only in tests, never in application functionality.

After building, `npm run preview` serves the frontend at `http://localhost:4173`
with the same API proxy. Preview is a local verification tool, not a production server.

## Architectural decisions

See [architectural conventions and module catalog](docs/architecture.md) for the
15 business module boundaries, DI lifetimes, naming, response/error contracts,
logging, validation, and accepted decision records.
See [domain entity conventions](docs/domain.md) for the implemented constructor
and lifecycle API. These methods enforce local invariants without adding business
endpoints or automatic cross-module workflows.
See [API conventions](docs/api.md) for canonical `/api/v1` routes, preserved `/api`
aliases, shared paging/filtering/sorting, error shapes and development Swagger UI.
Resource collections default to page 1, page size 20 (maximum 100).

### Clean architecture within a modular monolith

There is one API deployment, not a collection of microservices. Dependencies point inward:

```text
Application    -> Domain
Infrastructure -> Application, Domain
Api            -> Application, Infrastructure
Tests          -> Api (and its transitive dependencies)
```

The domain has no framework or persistence dependencies. Application interfaces
will define use-case boundaries; Infrastructure will implement them. API endpoints
will use application services and sealed request/response DTOs, never expose
database entities, and forward cancellation through asynchronous work.

The API is the DI composition root. Explicit `AddApplication`, `AddInfrastructure`
and `AddPresentation` registrations establish layer conventions, with DI scope and
build validation enabled. Infrastructure configures the selected scoped DbContext.
There are no placeholder business services.
Future features use `Modules/<Module>` within each layer, following the catalog,
rather than deploying separate services.

Minimal API validation uses built-in .NET 10 DataAnnotations support. HTTP
exceptions map to safe Problem Details, while ordinary programming exceptions
remain 500 errors. Request logging uses route templates, elapsed time and trace
scopes; production logs are JSON and development logs are readable single-line output.

### Tenant isolation boundary

Organization-level isolation is mandatory. Registration stores organizations and
memberships; the protected read-only resource API applies resource permissions.
For tenant-owned endpoints:

1. Resolve the active organization from an authenticated user's authorized memberships.
2. Reject missing or unauthorized organization context; never default to a tenant.
3. Require organization scope for reads and writes, and enforce it in persistence.
4. Do not trust organization IDs supplied in headers, query strings, or request bodies
   without authorization checks.
5. Add integration tests proving cross-organization access is denied.

Persistence enforces scoped queries/writes and composite organization foreign
keys. Authentication resolves only active memberships in active organizations.
`X-Organization-Id` is checked against membership; absent headers select an organization
only when there is exactly one authorized membership. Missing/ambiguous context fails closed.
Managers require property assignments; tenants require their own linked Tenant
records; contractors require assigned jobs and a linked active Contractor.
Platform access is a separate CSRF-protected, reason-required, audited read.
PostgreSQL RLS adds a transaction-local organization boundary; SQLite retains
application/EF isolation. See [authorization](docs/authorization.md).

### Vite and shadcn/ui

Vite is retained intentionally; Next.js and server-side rendering are not required.
The existing screen and visual design are preserved. Tailwind CSS 4 uses its Vite
plugin, semantic theme tokens match the existing design, and the refresh control
uses a locally owned shadcn/ui-style Button. `components.json` and the `@/` alias
allow additional requested components to be added using the shadcn CLI.

### Dependencies and deployment

Use built-in ASP.NET Core health checks, DI, and Problem Details. OpenAPI uses
Microsoft's ASP.NET Core package, not Swashbuckle. Its transitive `Microsoft.OpenApi`
dependency is explicitly pinned to a patched compatible 2.x version to avoid the
security advisory affecting the default dependency.

For deployment, serve the Vite build and reverse-proxy `/api` to the backend on
the same origin. The Vite proxy is not included in the built static files.
Configure HTTPS termination, actual `AllowedHosts`, and secrets in the deployment
environment. Cross-origin CORS is intentionally not enabled. EF Core PostgreSQL
and SQLite persistence, ASP.NET Core Identity and account email delivery are configured.
Production also needs protected persistent Data Protection keys and SMTP credentials;
see [deployment checklist](docs/authentication.md#production-checklist).
No message broker or container dependencies are introduced.

## Next development step

Implement requested organization membership/provisioning workflows with scoped
authorization and manager/contractor assignment management, then property/unit CRUD.
Verify the live PostgreSQL RLS test and least-privilege runtime role before production.