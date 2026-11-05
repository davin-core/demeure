# Demeure architectural conventions

Status: accepted for the foundation. Demeure is the product name; earlier prompts
using "Logis" refer to this same application.

This document defines boundaries and conventions, not implemented business
features. Implemented endpoints are health checks, Identity authentication, protected
read-only resources, explicitly audited platform reads and
development-only OpenAPI. Module directories are populated when their first
requested use cases are implemented; no placeholder entities, services, or routes
are needed to establish the architecture.

## 1. Clean Architecture and deployment

Demeure is a modular monolith: one ASP.NET Core API deployment with logical
business modules, plus one React + TypeScript + Vite frontend. Do not introduce
microservices, message brokers, service discovery, or inter-module HTTP calls.

| Layer | Owns | Must not own |
| --- | --- | --- |
| Domain | Entities, value objects, aggregates, pure business invariants | HTTP, DI, persistence mappings, external SDKs |
| Application | Use cases, application DTOs, module contracts, tenant-context and persistence interfaces | ASP.NET Core types, database/provider implementations |
| Infrastructure | Persistence, external integrations, implementations of application interfaces | HTTP endpoint policies or independent copies of domain rules |
| Api (presentation) | HTTP contracts, endpoint mapping, validation, middleware, composition root | Direct database access or business workflows |
| Frontend (presentation) | UI, API client, client-side UX validation | Authoritative authorization or domain rules |

The frontend's [application layouts](layouts.md) separate reusable presentation
shells, validated session API access and coarse navigation permissions. Client
organization selection never replaces live backend authorization. Layout-only
routes do not introduce business pages, new APIs or microservices.

The [frontend integration](frontend-integration.md) centralizes HTTP transport
and validates DTOs before rendering. Cookie login, CSRF handling, protected layout
gates and organization-keyed pagination remain presentation concerns. No frontend
role checks, cached counts or route guards replace backend authorization.

Project references:

```text
Demeure.Domain         -> none
Demeure.Application    -> Demeure.Domain
Demeure.Infrastructure -> Demeure.Application, Demeure.Domain
Demeure.Api            -> Demeure.Application, Demeure.Infrastructure
```

The API's Infrastructure reference is for composition only. Endpoint handlers
depend on Application interfaces, not concrete Infrastructure classes.
`Demeure.Tests/Architecture/LayerDependencyTests.cs` checks the project-reference
graph and ensures Domain has no package or framework dependencies.

The domain remains framework-free. Application uses the small Microsoft DI
abstractions package for registration. Infrastructure additionally owns EF Core
and the PostgreSQL/SQLite providers. Infrastructure now also references
`Microsoft.AspNetCore.App` and Identity EF stores for authentication adapters;
Domain and Application remain independent of ASP.NET Core. Endpoint policies
stay in Api; Identity account/store/cookie integrations stay in Infrastructure.
See the [database design](database.md) for entities, constraints,
provider configuration and migrations.

## 2. Module organization and ownership

The following names are canonical folder and namespace segments. Each module
owns its rules, use cases, contracts, and eventual persistence mappings. This is
logical ownership, not a commitment to separate databases or deployments.

| Module | Owns | Boundary and collaboration |
| --- | --- | --- |
| Organizations | Organizations, authorized memberships, organization lifecycle and settings | Provides organization authorization/context; membership references Users identities. |
| Users | User identity, account profile and identity-provider integration | Identity can be global; never grants organization access without Organizations membership. |
| Properties | Buildings/properties and property-level metadata | Organization-scoped; Units refers to property IDs rather than owning property records. |
| Units | Rentable units and unit-level characteristics | Organization-scoped; validates referenced Properties through an application contract. |
| Tenants | Resident/tenant records and contact information | Organization-scoped resident data, **not** the SaaS organization-isolation mechanism; optionally links to a Users identity. |
| Applications | Rental applications and their lifecycle | Organization-scoped; references Units and Tenants through explicit contracts; distinct from the Application architecture layer. |
| Leases | Lease agreements, terms and lifecycle | Organization-scoped; owns leasing rules and references Units/Tenants without modifying their internals. |
| Payments | Rental/customer payment records and allocations | Organization-scoped; cooperates with Leases; does not own SaaS subscription billing. |
| Expenses | Property operating expenses and classification | Organization-scoped; references Properties/Units where applicable; separate from incoming Payments. |
| Maintenance | Maintenance requests, work orders and assignment lifecycle | Organization-scoped; references Units/Properties and authorized Contractors. |
| Contractors | Service-provider records and capabilities | Organization-scoped; Maintenance consumes contractor contracts, not its persistence objects. |
| Documents | Document metadata, storage interfaces and authorized retrieval | Organization-scoped business attachments; owning modules decide attachment permissions, Documents enforces retrieval authorization. |
| Notifications | Delivery requests, preferences and delivery adapters | Uses minimal authorized payloads; does not decide business outcomes or become the source of record. |
| Reporting | Organization-scoped projections and analytical queries | Read-only consumer of explicit query contracts/projections; cannot bypass organization filtering. |
| Subscriptions | Organization SaaS subscriptions, plans and entitlements | Platform billing, separate from rental Payments; privileged plan administration must have explicit authorization. |

Use the same module segment in each applicable layer:

```text
backend/
  Demeure.Domain/Modules/<Module>/
    Entities/
    ValueObjects/
  Demeure.Application/Modules/<Module>/
    Contracts/
    UseCases/<UseCase>/
  Demeure.Infrastructure/Modules/<Module>/
    Persistence/
    Integrations/
    DependencyInjection.cs
  Demeure.Api/Modules/<Module>/
    Contracts/
    <Module>Endpoints.cs
frontend/src/features/<module-kebab-case>/
backend/Demeure.Tests/Unit/<Module>/
backend/Demeure.Tests/Integration/<Module>/
```

Create only folders used by implemented code. Not every module needs every
subfolder. Existing health contracts/endpoints remain outside `Modules` because
they are operational concerns rather than a business module.

### Cross-module rules

- Use explicit Application contracts and immutable DTOs for collaboration.
- Never access another module's repositories, concrete services, tables, tracked
  entities, or private domain types directly.
- Expose only intended module contracts publicly; keep implementations internal
  where possible. One project per layer does not enforce module visibility
  automatically, so enforce these rules in reviews and add targeted tests as
  actual module relationships appear.
- Pass IDs/value objects across boundaries rather than shared mutable aggregates.
- Inject required contracts directly. Do not introduce a mediator or event bus
  merely to hide dependencies.
- Keep dependencies directional and acyclic. A use case that coordinates modules
  belongs to the module responsible for the workflow; do not add reciprocal calls.
- Reporting uses authorized query contracts or explicitly designed read models,
  not arbitrary access to every module's persistence.
- Notification delivery occurs through its contract after the owning workflow's
  successful state change. Reliable delivery/outbox design is deferred until
  persistence and a delivery requirement exist.
- Define transaction boundaries for each persistent use case when it is added.
  Do not introduce speculative distributed transactions.

### Organization isolation

Organization context is the Application `IOrganizationContext` abstraction.
Persistence-only registration supplies an unavailable scoped implementation.
Authentication registration supplies a request-scoped context whose internal
setter is used only after membership validation. The middleware selects a supplied
organization only if authorized, or the sole active membership when unambiguous;
client headers cannot populate it directly.

For tenant-owned data:

1. Reject missing/unauthorized context; never default to an organization.
2. Scope every read, write, lookup, background job, report, cache key and document
   retrieval to its authorized organization.
3. If a route or request supplies an organization ID, compare it to authorized
   membership/context; the identifier itself grants no access.
4. A resource belonging to another organization must not become accessible by
   changing its ID. Prefer a scoped lookup returning 404 to prevent existence leaks.
5. Model organization ownership and enforce persistence constraints when the
   database is added. Query filters alone are not an authorization mechanism.
6. Global identity/platform operations require separately explicit authorization;
   do not turn "global" into a tenant-filter bypass.
7. Add two-organization integration tests before exposing tenant-owned endpoints.

Persistence, authentication and authorized context resolution are implemented.
Business endpoints still require use-case authorization. Query filters, SaveChanges ownership checks
and organization-scoped composite FKs provide persistence defense in depth.
Resource reads additionally enforce property assignments, resident ownership and
contractor job assignments. PostgreSQL RLS forces an organization boundary on
business tables using a transaction-local authenticated context. See
[authorization](authorization.md) for scope, bootstrap exclusions and deployment requirements.

## 3. Dependency injection and service conventions

`Program.cs` is the composition root:

```csharp
builder.Services
    .AddApplication()
    .AddInfrastructure(databaseProvider, connectionString)
    .AddPresentation();
```

Application owns framework-free authentication DTOs/service contracts. Infrastructure explicitly registers
the configured provider's scoped DbContext, Identity services and scoped authentication/context adapters.
Provider/connection configuration is required and validated; there is no silent
database fallback. Add explicit module service registrations as workflows are
implemented. Presentation registers health checks, OpenAPI, validation, logging
support and Problem Details. The API also calls `AddAuthenticationConfiguration`,
which composes Infrastructure authentication with presentation-owned CSRF,
rate limiting, role policies and environment validation.

- Use constructor injection and register services against purpose-specific interfaces.
- Use `AddScoped<IService, Service>()` for request/use-case services, future tenant
  context, repositories and database contexts.
- Use singleton only for immutable, thread-safe services with no scoped dependencies.
  The stateless Problem Details writer is a singleton.
- Use transient for lightweight stateless helpers only when a distinct service is
  actually warranted; prefer ordinary functions for trivial transformations.
- Domain objects are created by domain/application code, not registered in DI.
- Never call `BuildServiceProvider()` during registration, inject `IServiceProvider`
  as a service locator, store a scoped service statically, or use assembly scanning
  to conceal module registrations. A composition-root registration factory may
  resolve the SQLite context to alias it as the shared DbContext; business services
  may not resolve their own dependencies.
- Scope/build validation is enabled in every environment to fail early on invalid
  dependency lifetimes and missing constructor dependencies.
- Async I/O methods use the `Async` suffix and accept/forward `CancellationToken`.
  Do not block with `.Result`/`.Wait()` or use `Task.Run` to wrap normal server I/O.

## 4. API contracts and responses

Continue using minimal APIs, grouped in `<Module>Endpoints` static mapping classes.
Do not mix controllers into the API without a documented decision.

- Resource paths use `/api`, lowercase plural resource names and kebab-case segments.
- Use HTTP verbs, not action names in ordinary CRUD routes.
- Endpoints accept `CancellationToken` and delegate workflows to Application services.
- Prefer `TypedResults`; declare `Results<TSuccess, TError>` for multiple return types.
- Use sealed immutable records for HTTP DTOs, camelCase JSON, and XML summaries for
  API-exposed types. Request records use `init` properties and validation attributes.
- Use `DateTimeOffset` for instants. Serialize enums as strings. Do not expose
  persistence entities, password fields, provider exceptions or internal state.
- Existing JSON conventions remain: exact property casing and duplicate properties
  rejected. Do not tighten or change established serialization without tests.
- Return the DTO directly for successful single-resource responses. Do not add a
  blanket `{ success, data, error }` envelope.
- Collections use `PagedResponse<T>` with items/page/pageSize/totalCount/totalPages.
  Apply authorization before the shared SQL filter/count/sort/page helper; defaults
  are page 1/pageSize 20, maximum 100. Reject unsupported/duplicate query parameters.
  See [API conventions](api.md) for status allowlists and deterministic sorting.
- Describe each endpoint's response DTOs, status codes, summary and security
  requirements in OpenAPI. OpenAPI remains Development-only.

| Operation | Success | Errors when applicable |
| --- | --- | --- |
| GET | 200 with DTO/list | 400, 401, 403, 404 |
| POST create | 201 with DTO and `Location` | 400, 401, 403, 409 |
| PUT/PATCH | 200 with DTO | 400, 401, 403, 404, 409 |
| DELETE | 204, no body | 401, 403, 404, 409 |

Authentication returns 401 for unauthenticated callers and 403 for authenticated
callers lacking permission. Cookie handlers never redirect API failures to HTML.

### Error responses

Use `application/problem+json` Problem Details, with `type`, `title`, `status`,
`instance` (path only, no query string), and `traceId`. Validation adds `errors`,
mapping invalid field/parameter names to message arrays.

Example shape:

```json
{
  "type": "about:blank",
  "title": "Resource not found",
  "status": 404,
  "instance": "/api/properties/example-id",
  "traceId": "example-trace-id"
}
```

The single `ApiProblemDetailsWriter` customizes all framework-generated problems,
including validation and status-code pages, and emits JSON even when the caller
sends an incompatible `Accept` header. Errors must not fall into an HTML developer
exception page or fail merely because of content negotiation.

Health responses intentionally preserve the operational `{ status, service }`
contract, including readiness's HTTP 503 response. They are probe results, not
business errors, and are not converted to Problem Details.

## 5. Exceptions and middleware

Pipeline order:

```text
Routing -> request log/trace scope -> exception handler -> status-code pages
-> resource no-store metadata -> authentication -> rate limiter -> CSRF
-> authorized organization context -> authorization -> collection-query validation
-> endpoint binding/validation -> endpoints
```

Routing runs first so request logs can capture a route template rather than a raw
resource path. The logger captures it before exception handling clears endpoint
metadata. Authentication and authorization remain inside the logging/error boundary.
All unsafe `/api` requests require CSRF, including anonymous account operations.

`ApiExceptionHandler` is the one application exception-to-HTTP boundary:

| Exception | HTTP | Safe title |
| --- | --- | --- |
| `ResourceNotFoundException` | 404 | Resource not found |
| `ConflictException` | 409 | Request conflicts with the current resource state |
| `ForbiddenException` | 403 | Access denied |
| `RequestValidationException` | 400 with field errors | Validation failed |
| `BadHttpRequestException` | Framework-specified request status (e.g. 400/415) | Invalid HTTP request |
| Unexpected exception | 500 | An unexpected error occurred |

These application exceptions are framework-independent and carry diagnostic
messages for protected server logs, never public response details. Do not put
secrets or personal data in their messages.

Only use the named application exceptions for explicitly identified failures.
Do not map every `ArgumentException`, `InvalidOperationException`, or provider
exception to a client error: these may indicate programming/infrastructure bugs.
For simple endpoint branches, a typed NotFound/Conflict result is also appropriate;
do not use exceptions for routine branching unnecessarily.

Binding errors are configured to throw consistently in Development and Production
so malformed JSON becomes a safe 400, not an unexpected 500. Actual request
cancellation is left to the framework; do not log a disconnected caller as a
business failure or treat unrelated cancellation as successful work.

Expected exceptions are logged at Warning; unexpected failures at Error.
.NET exception-handler diagnostics are suppressed for handled exceptions because
the custom handler owns logging, avoiding duplicate exception logs.

Do not add broad catches in endpoints, silently ignore errors, or return empty
success-shaped defaults. Infrastructure may translate narrowly identified provider
failures into Application errors, preserving the original cause where needed.

## 6. Logging and correlation

Use `ILogger<T>` and structured templates:

```csharp
logger.LogInformation("Lease {LeaseId} updated in organization {OrganizationId}", leaseId, organizationId);
```

This is a convention example, not a feature currently implemented.

- Information: request completion and important state transitions.
- Warning: known rejected operations and recoverable operational issues.
- Error: unexpected failures with exception context.
- Debug: detailed non-sensitive diagnostics, normally disabled in production.
- Use structured named properties, not interpolated log strings.
- Request logs record method, route **template**, status and elapsed milliseconds.
  A scope carries the framework trace ID; Problem Details uses the same value.
- Use the framework's distributed tracing identity, not an unchecked custom
  `X-Correlation-ID` header. Do not add telemetry dependencies without a requirement.
- Never log authorization headers, cookies, credentials, request bodies, query
  strings, raw payment/document content, or resident personal data.
- Organization middleware adds the authorized organization ID to the downstream
  logging scope, not an unvalidated client value.
- Production console logs are JSON with UTC timestamps and scopes; Development
  uses single-line readable console logs. Configure sinks/retention at deployment;
  no file logger or third-party logging package is introduced.

## 7. Validation

Use the built-in .NET 10 minimal API validation registration, `AddValidation()`.
No FluentValidation or mediator dependency is necessary.

1. API DTOs validate request shape with DataAnnotations (`Required`, `MaxLength`,
   `Range`, etc.). C# `required` is a construction/serialization constraint, not a
   substitute for runtime business validation.
2. Automatic validation runs before the endpoint handler and returns a 400
   validation problem with field errors. Validate nested request types when used
   and test the exact response shape.
3. Application use cases validate contextual rules, referenced resources and
   authorized organization ownership. They may be invoked without HTTP.
4. Domain aggregates/value objects enforce invariants regardless of entry point.
5. Persistence adds uniqueness/foreign-key constraints when a database is introduced.
6. Frontend validation improves UX but never replaces backend validation.

The validation generator must be registered in the assembly defining the endpoint
types. Production endpoints belong to the API assembly, which calls AddValidation;
test-only endpoints call AddValidation in their own assembly too. Integration
tests verify both annotated scalar parameters and annotated DTO properties, not
merely that validation services were registered.

Do not enable experimental validation annotations or suppress analyzer diagnostics
to get validation working. Add validators only alongside actual requested DTOs.

## 8. Naming and code organization

| Concern | Convention |
| --- | --- |
| Product/projects | `Demeure`, `Demeure.<Layer>` |
| Namespace | `Demeure.<Layer>.Modules.<Module>.<Concern>` |
| C# files/types | PascalCase, one main type per file |
| Interfaces | `I` prefix, purpose-specific (e.g. `IPropertyService`) |
| Async methods | `<Verb>Async`, final `CancellationToken` parameter |
| HTTP requests | `Create<Entity>Request`, `Update<Entity>Request` |
| HTTP responses | `<Entity>Response`, `<Entity>ListResponse` |
| Endpoint classes | `<Module>Endpoints`, `Map<Module>` extension |
| Private/local names | camelCase |
| React components | PascalCase; keep existing shadcn lowercase component filenames |
| Frontend module folders | kebab-case under `src/features` |
| Tests | `<TypeOrFeature>Tests`; method names describe behavior and outcome |

Use `Common` only for genuinely shared primitives/policies, not a dumping ground
for module-specific logic. Keep health DTOs in API Contracts, middleware in
Middleware, and HTTP-only helpers in presentation. Prefer small purpose-specific
interfaces over generic base-service/repository hierarchies.

## 9. Testing and enforcement

- Unit-test domain invariants and application use cases as they are implemented.
- Use WebApplicationFactory for HTTP integration tests and real persistence when
  persistence is added; test doubles must remain test-only.
- Current architecture tests check layer references and Domain independence.
- Current convention tests cover DI registration, safe exception mappings,
  correlation, single exception logging, route-template logging, malformed JSON,
  incompatible Accept headers and validation before handler execution.
- Existing health tests must continue to pass, preserving contracts and OpenAPI.
- Extend module-isolation tests with each concrete collaboration; do not claim that
  the project-reference test alone enforces every namespace boundary.
- Run the smallest relevant tests, then the backend suite for shared middleware
  changes. See the [root README](../README.md) for commands.

## 10. Decision record

| Decision | Rationale | Consequence |
| --- | --- | --- |
| ADR-001: modular monolith with four Clean Architecture layers | Keep deployment simple and domain independent | Logical module boundaries need explicit contracts and review; no microservices. |
| ADR-002: explicit DI composition and lifetime validation | Make dependencies visible and fail early | Layer registration extensions, no scanning/service locator. |
| ADR-003: direct DTO successes and Problem Details failures | Follow HTTP conventions without redundant envelopes | All clients use HTTP status and safe correlated error bodies. |
| ADR-004: built-in validation and logging | Avoid unnecessary dependencies | DataAnnotations plus application/domain rules; ILogger and console formatters. |
| ADR-005: organization authorization before tenant data | Prevent accidental cross-organization access | No tenant-owned endpoint before authentication/context and isolation tests. |
| ADR-006: module catalog before business implementation | Establish ownership without speculative workflows | Populate folders when code is requested; initial domain records now support the requested schema. |
| ADR-007: preserve Vite and operational contracts | Respect the existing working foundation | No Next.js migration; health response remains unchanged. |
| ADR-008: PostgreSQL Code First with SQLite development | Keep PostgreSQL production semantics while allowing local use without a server | Separate migrations; exact SQLite money/UTC conversions; live PostgreSQL verification required before production. |
| ADR-009: composite organization FKs and fail-closed context | Prevent cross-organization relationships at persistence boundary | Tracked guards/filters supplement FKs; authenticated context and use-case authorization remain mandatory. |

Update this document when a decision changes. Background delivery guarantees and
payment providers remain undecided. PostgreSQL is the production database and
SQLite is the local development/test alternative.

### ADR-011: Identity adapters and same-origin cookie authentication

ASP.NET Core Identity is an Infrastructure adapter with a separate `IdentityAccount`
sharing the Domain User GUID. Password hashes/security stamps never enter Domain
or API responses. Infrastructure's ASP.NET framework reference is necessary for
these adapters; no ASP.NET dependencies enter Domain/Application.

Use HttpOnly same-origin cookies plus CSRF, not JWT; no refresh-token table is
needed. Public registration creates a new owner organization atomically, never
self-selected roles. Only SuperAdministrator is a global role. Organization roles
are recalculated from active membership on every request; no global Identity
organization role grants tenant access. Claims/stamp validation cannot renew the
fixed eight-hour ticket. See [authentication](authentication.md) for constraints,
email failure recovery and operational requirements.

### ADR-012: scoped resource reads and explicit platform access

Use one explicit read-authorization facade for the initial seven resource types,
returning a discriminated union of sealed DTOs rather than entities. Owners read
within their organization; manager assignments and tenant/contractor login links
restrict the other roles. No CRUD or provisioning is implied.

Platform privileges are separate policies and per-operation route targets with
mandatory reason/committed audit records, not a blanket tenant-filter bypass.
The existing member actor FK remains; a separate optional global platform actor
supports nonmember administrators without weakening it.

PostgreSQL business tables use FORCE RLS and parameterized transaction-local
organization/user/platform settings, plus live membership/global-role verification.
Organization/membership/Identity bootstrap tables remain outside RLS and are
accessible only through narrowly scoped application workflows. Runtime roles
must not own schemas or bypass RLS. SQLite enforces application/EF scope without
claiming database RLS. Live PostgreSQL verification remains required.

### ADR-013: reusable API contracts, v1 routes and local Swagger UI

Canonical business routes use `/api/v1`, with unversioned compatibility aliases
sharing handlers, cookies and authorization. Operational health routes stay
unversioned. A breaking future version requires separately mapped contracts, not
silent negotiation or fallback. Alias removal is a separately announced decision.

Shared `CollectionQuery`/`PagedResponse<T>` contracts and one EF helper apply typed
status filtering, allowlisted sorting and SQL pagination only after resource
authorization. Count/items share a transaction snapshot; no client SQL or
in-memory pagination is accepted. Request semantic errors use the existing
validation Problem Details boundary.

Retain Microsoft OpenAPI; add only pinned local Swagger UI assets through a small
API-local npm project. No Swashbuckle, CDN, new runtime backend framework or
frontend authentication screens are needed. OpenAPI/Swagger remain development-only.
See [API conventions](api.md) for parameters, version compatibility, install/build
instructions and the complete change inventory.

### ADR-010: behavior-rich initial domain entities

Organization, User, OrganizationMembership, Property, Unit, Tenant, Lease and
Payment now use validated constructors, private business setters and explicit
lifecycle methods. Relationships remain immutable/scoped IDs; Domain has no EF
attributes, provider logic or service dependencies. See the
[domain decisions](domain.md) for the accepted transition matrix, advance lease
activation, scheduled end-date checks, full-refund behavior and deferred
cross-aggregate rules. Invalid operations are rejected atomically.
