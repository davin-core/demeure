# Demeure API conventions

The API uses .NET 10 minimal APIs, not MVC controllers. Conventions are shared
through route groups, endpoint metadata, middleware, Application query contracts
and one EF query helper. No controller hierarchy, mediator, versioning framework
or alternate OpenAPI generator is introduced.

## Versioning and REST routing

**Canonical business routes use `/api/v1`.** Authentication, organization resource
reads/collections and explicitly audited platform reads are mapped once to a
versioned group and once to unversioned `/api` compatibility aliases.

- Existing clients keep working: cookies, CSRF, authorization, rate limits, JSON
  shapes and unversioned registration Location remain unchanged.
- Versioned registration returns Location `/api/v1/auth/session`; compatibility
  registration continues returning `/api/auth/session`. Registration does not sign in.
- Alias endpoints have unique suffixed names and are excluded from OpenAPI to
  avoid duplicating every operation. No removal date is declared; removing aliases
  requires a separately communicated compatibility decision.
- Only v1 is implemented. `/api/v2/...` does not silently fall back to v1.
  Future breaking contracts get an explicit new route group, document and tests;
  old contracts remain independently mapped during their announced support period.
- Operational probes `/api/health` and `/api/health/live` stay unversioned, preserving
  existing frontend/probe contracts. `/openapi/v1.json` names the v1 document.
- Ordinary resource paths are plural, lowercase/kebab-case nouns. Use GET for
  reads, POST for creation, PUT/PATCH for requested updates, DELETE for deletion.
  Existing authentication/session and audited platform access are explicit actions.

Use endpoint name/route metadata for link generation when future create endpoints
need it; do not hard-code unversioned Location paths in new versioned handlers.
All I/O endpoints accept and forward request cancellation.

## DTOs and response shapes

Successes return immutable DTOs directly, not a blanket success/data/error envelope.
Requests are sealed records with `init` properties and DataAnnotations.
Endpoints never serialize entities, credentials or provider errors.

The existing read contract is a discriminated `ResourceResponse` union; its
concrete DTOs are sealed. Collections preserve that item contract:

```json
{
  "items": [
    {
      "resourceType": "property",
      "id": "00000000-0000-0000-0000-000000000001",
      "organizationId": "00000000-0000-0000-0000-000000000002",
      "name": "Example property",
      "city": "Montreal",
      "status": "Active"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

The example is documentation, not seed data. Money is decimal, status strings are
enum names, DateTimeOffset is used for instants and DateOnly for calendar dates.
Existing camelCase JSON, exact property casing and duplicate-property rejection
are unchanged. Public DTO types have XML summaries.

All resource responses, including authorization/binding errors, have
`Cache-Control: no-store`, applied from endpoint metadata before authentication.
Authentication session/CSRF endpoints retain their existing no-store behavior.
Collection success is 200, even when no records match or a page lies beyond the
result set. A single missing/foreign/unassigned identifier returns 404; anonymous
callers get 401 and callers without required organization policy get 403.

## Collection query contract

Protected GET collections now exist for all seven read resources:

```text
/api/v1/properties
/api/v1/units
/api/v1/tenants
/api/v1/rental-applications
/api/v1/leases
/api/v1/payments
/api/v1/maintenance-requests
```

The matching unversioned `/api` paths are compatibility aliases.
There are no platform bulk/list endpoints: platform access remains per-record
with a reason and audit.

| Parameter | Default | Accepted values |
| --- | --- | --- |
| `page` | 1 | One-based positive integer; computed offset must fit Int32 |
| `pageSize` | 20 | 1-100, inclusive; never silently clamped |
| `status` | No filter | Exact case-sensitive status name for this resource |
| `sortBy` | `id` | `id`, `createdAt`, `status` |
| `sortDirection` | `asc` | `asc`, `desc` |

Every supplied parameter must have exactly one value. Unknown parameters, blank
values, invalid bounds, unsupported sort fields/directions, numeric enum values
and invalid statuses are rejected with 400. Malformed integer syntax uses the
existing framework request-error boundary; semantic validation adds field errors.
Do not accept arbitrary property names, SQL expressions or client sort snippets.

`CollectionParameters` binds HTTP primitives and converts them to the
framework-independent `CollectionQuery`. Its validation is reusable by non-HTTP
callers. A metadata-selected middleware rejects duplicates/unknown parameters
before binding (duplicate integers must not become an ambiguous comma-separated
parse). The EF helper validates status against the entity's actual enum and builds
a typed expression, not raw SQL or in-memory filtering.

Status allowlists:

| Resource | Status values |
| --- | --- |
| Properties | Active, Inactive, Archived |
| Units | Available, Occupied, Unavailable, Archived |
| Tenants | Active, Inactive, Archived |
| Rental applications | Draft, Submitted, UnderReview, Approved, Rejected, Withdrawn |
| Leases | Draft, Active, Ended, Terminated, Cancelled |
| Payments | Pending, Completed, Failed, Cancelled, Refunded |
| Maintenance | Open, Assigned, InProgress, Completed, Cancelled |

For non-ID sorts, ascending ID is the deterministic secondary sort, regardless
of the primary direction. Status sorting follows the database's stored status
text order, not lifecycle order. No custom multi-field sort language is introduced.
`totalPages` is ceiling(totalCount/pageSize); zero matching records means zero
pages. Beyond-end pages retain the authorized filtered total but have empty items.

Filtering and authorization happen **before counting, sorting and pagination**.
`QueryConventions` executes the filtered count and bounded page in SQL, then
projects DTOs. It never loads all records and paginates in memory. The same
resource authorization predicate/projection is shared by single, collection and
controlled platform reads. Counts cannot disclose foreign or unassigned records.

Collections use a snapshot-consistent transaction: RepeatableRead on PostgreSQL,
Serializable on SQLite. Count and items therefore share one database snapshot.
PostgreSQL transaction-local RLS context remains mandatory and is cleared by
commit/rollback. Membership checks occur at the start of each request, not in a
long-lived cached identity. Offset paging is bounded and appropriate for this
initial scope; keyset pagination/advanced search is not implemented.

## Validation and exception handling

- Minimal API `AddValidation()` validates annotated request DTOs before handlers.
- Application query/use-case validation returns `RequestValidationException`.
- The existing exception handler maps known request failures to safe
  `application/problem+json`, logs them once and supplies `traceId` and path-only
  `instance`. Semantic validation adds `errors`; internal messages are not exposed.
- Unexpected exceptions remain 500; do not blanket-convert ArgumentException or
  InvalidOperationException to client errors.
- Authorization stays before collection binding/execution. Unknown queries do
  not reveal records to unauthenticated callers.
- Framework parsing failures and status-code pages use the same JSON writer.
  Health failures intentionally preserve their operational response instead.

## OpenAPI and Swagger UI

Microsoft's built-in generator continues to serve `/openapi/v1.json` in
Development. Shared transformers declare v1, cookie authentication, organization
selection, required CSRF headers and relevant error/rate-limit responses.
Collection parameters, response schemas and operation summaries are documented.
Only canonical business paths and existing operational health paths appear.

Development-only **Swagger UI** is at `/swagger` (redirects to `/swagger/index.html`).
No CDN, online validator, Swashbuckle, NSwag or external runtime documentation
service is used. `swagger-ui-dist` 5.34.1 is an exact-pinned UI asset dependency
with an npm lockfile; the backend's npm project is separate from the Vite frontend.
Its lifecycle scripts are disabled in `.npmrc`, including the transitive
telemetry-install helper.

From the repository root, before the first backend build:

```powershell
npm ci --prefix backend\Demeure.Api
dotnet build Demeure.slnx
dotnet run --project backend\Demeure.Api
```

The build fails explicitly when UI assets are missing. MSBuild copies only the
required UI JS/CSS, local initializer and license into output/publish; it does not
publish node_modules. Production routes do not expose UI or OpenAPI even though
the published files are present. The initializer/index use local same-origin
assets and a content-security policy.

Swagger sends same-origin cookies. To sign in, register/verify through the real
account flow, then execute login. The HttpOnly session cookie is automatically
sent; never paste it into an authorization dialog or localStorage.
For unsafe requests, the interceptor fetches a fresh `/api/v1/auth/csrf` token
and supplies it automatically. A UI-only parameter macro fills the required
header's form placeholder so execution can reach that interceptor; that placeholder
is **not** a valid API token. CSRF fetch/schema failures stop the request explicitly.
No authentication bypass or test account is added.

Use Swagger only on an appropriately restricted development listener; it sends
real requests and may modify data through existing authentication actions.
Its displayed requests may include CSRF tokens; do not share private request
examples or credentials. Production HTTPS/identity requirements are unchanged.

## Verification and remaining limits

Tests cover defaults/maxima/overflow, semantic and binding failures, typed status
filters, all seven collections under every supported role, count isolation,
deterministic paging/sorting, version aliases, Location compatibility, canonical
OpenAPI/security schemas, local Swagger assets and production documentation gating.
Real browser verification exercises the UI and fresh-CSRF recovery request flow.

No database schema changes are needed. Live PostgreSQL RLS execution remains
pending without a server; SQL/model checks do not substitute for it. No CRUD,
full-text search, arbitrary filters, multi-sort DSL, v2 API, or platform bulk export
is added. Future list use cases should apply the shared helper **after** their
operation-specific authorization predicates.

## Change inventory

Created:

- [CollectionQuery.cs](../backend/Demeure.Application/Common/Queries/CollectionQuery.cs)
- [QueryConventions.cs](../backend/Demeure.Infrastructure/Persistence/QueryConventions.cs)
- [CollectionParameters.cs](../backend/Demeure.Api/Contracts/CollectionParameters.cs)
- [ApiConventions.cs](../backend/Demeure.Api/Extensions/ApiConventions.cs)
- [OpenApiConventions.cs](../backend/Demeure.Api/Extensions/OpenApiConventions.cs)
- [CollectionQueryMiddleware.cs](../backend/Demeure.Api/Middleware/CollectionQueryMiddleware.cs)
- [ResourceResponseMiddleware.cs](../backend/Demeure.Api/Middleware/ResourceResponseMiddleware.cs)
- [DocumentationEndpoints.cs](../backend/Demeure.Api/Endpoints/DocumentationEndpoints.cs)
- [swagger-init.js](../backend/Demeure.Api/Documentation/swagger-init.js)
- [package.json](../backend/Demeure.Api/package.json), [package-lock.json](../backend/Demeure.Api/package-lock.json),
  [.npmrc](../backend/Demeure.Api/.npmrc)
- [CollectionEndpointTests.cs](../backend/Demeure.Tests/Integration/CollectionEndpointTests.cs)
- [CollectionQueryTests.cs](../backend/Demeure.Tests/Unit/CollectionQueryTests.cs)
- [ApiDocumentationTests.cs](../backend/Demeure.Tests/Integration/ApiDocumentationTests.cs)
- This guide.

Modified:

- [ResourceContracts.cs](../backend/Demeure.Application/Common/Authorization/ResourceContracts.cs)
- [ResourceReader.cs](../backend/Demeure.Infrastructure/Authorization/ResourceReader.cs)
- [OrganizationDataTransaction.cs](../backend/Demeure.Infrastructure/Authorization/OrganizationDataTransaction.cs)
- [AuthenticationService.cs](../backend/Demeure.Infrastructure/Authentication/AuthenticationService.cs)
- [ResourceEndpoints.cs](../backend/Demeure.Api/Endpoints/ResourceEndpoints.cs)
- [AuthEndpoints.cs](../backend/Demeure.Api/Modules/Users/AuthEndpoints.cs)
- [presentation DI](../backend/Demeure.Api/Extensions/DependencyInjection.cs)
- [CsrfMiddleware.cs](../backend/Demeure.Api/Middleware/CsrfMiddleware.cs)
- [Program.cs](../backend/Demeure.Api/Program.cs), [Demeure.Api.csproj](../backend/Demeure.Api/Demeure.Api.csproj),
  [Demeure.Api.http](../backend/Demeure.Api/Demeure.Api.http)
- [README.md](../README.md), [architecture.md](architecture.md), [authentication.md](authentication.md),
  [authorization.md](authorization.md).
