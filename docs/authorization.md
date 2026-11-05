# Organization and resource authorization

Demeure remains a Clean Architecture modular monolith. Authorization has three
boundaries: authenticated organization selection, resource-scoped application
queries, and PostgreSQL organization Row Level Security (RLS).
SQLite uses the same application/EF boundaries but has no database RLS.
No membership, assignment or business CRUD endpoints are added in this stage.

The API-conventions stage adds canonical `/api/v1` routes and authorized paged
collections for all seven read resources. The `/api` routes below remain compatible
aliases; new callers should prefer v1. Single and collection reads share resource
predicates, and collection counts cannot expose foreign/unassigned data. See
[API conventions](api.md) for status filters, sorting, limits and response shapes.

## Organization context

An authenticated account may have multiple active organization memberships.
Only memberships in active organizations are selected:

- One membership and no header: select that organization.
- Multiple/no memberships and no header: leave context unavailable; protected
  organization reads return 403.
- `X-Organization-Id`: require one nonempty GUID and a matching active membership.
  Malformed values return 400; unauthorized values return 403.
- The selected role is recalculated each request. Cookie claims never store
  organization roles, and suspension/removal is effective on the next request.

The header is a selector, not a permission. Changing a resource ID cannot change
the selected organization. EF filters scope the lookup to that organization;
wrong-organization, unassigned and nonexistent identifiers all return the same 404.
Anonymous callers get 401. Ordinary Members have no resource-read policy.

## Resource permissions

| Membership | Read scope |
| --- | --- |
| Owner | All supported resources within the selected organization |
| Administrator / Manager | Assigned properties, their units, leases, payments, applications and maintenance; resident profiles linked through those leases/applications/jobs |
| Tenant | The Tenant profile linked to their UserId, its rental applications, leases, lease payments and maintenance requests with that TenantId |
| Contractor | Maintenance requests assigned to an active Contractor linked to their UserId |
| Member | No supported resource reads |

Managers do not gain all-organization access merely by having a role name.
`PropertyManagerAssignment` is an organization-scoped many-to-many relationship
between property and member user. A unique `(OrganizationId, UserId, PropertyId)`
index prevents duplicate assignment. Composite FKs reject foreign properties and
nonmember users. Removing an assignment removes access on the next read.

`Contractor.UserId` optionally references membership in the same organization;
there is at most one contractor login link per organization/user. No email matching
is used for authorization. Removing the link, changing the job's assignment or
deactivating the contractor removes access. Tenant and Contractor identities may
exist without login links; they then have no self-service account permissions.

Roles are exclusive membership roles in the current model. An assignment alone
does not elevate a Member/Tenant/Contractor to manager. Historic records remain
readable while their relevant membership/link/assignment authorizes them; lease
dates or status are not used as an implicit logout mechanism.

`IResourceReader` is an explicit authorization/read facade for this initial
cross-module read surface. It projects only immutable DTOs, never entities or
credentials. The base response contract is an abstract, discriminated DTO union;
its seven concrete DTOs are sealed records. This deliberate shared contract keeps
the same authorization behavior for normal and platform reads without duplicating
resource queries. It does not grant cross-module write access.

Do not expose arbitrary DbContext queries from new endpoints. EF's universal
filters enforce **organization** isolation, not every resource permission.
Future lists/writes/documents/reports must reuse or extend these resource scopes
and add their operation-specific policies before exposure.

## Read-only HTTP endpoints

All ordinary reads require the `OrganizationResource` policy, followed by the
service's live membership and resource-level check:

```text
GET /api/properties/{id}
GET /api/units/{id}
GET /api/tenants/{id}
GET /api/rental-applications/{id}
GET /api/leases/{id}
GET /api/payments/{id}
GET /api/maintenance-requests/{id}
```

Responses are non-cacheable JSON with a `resourceType` discriminator, `id`,
`organizationId` and the resource's documented fields. Error responses follow the
existing safe Problem Details convention. Calendar dates remain DateOnly; money
remains decimal. The [HTTP examples](../backend/Demeure.Api/Demeure.Api.http)
show each endpoint and organization selector.

## Explicit platform access

SuperAdministrator never automatically bypasses ordinary organization policies,
membership checks or EF filters. Controlled platform reads are separate:

```text
POST /api/platform/organizations/{organizationId}/{resource-path}/{id}/access
{ "reason": "Investigating support incident 123" }
```

The resource paths match the seven ordinary read paths above. Each call requires:

1. The separate `PlatformResource` policy and a live SuperAdministrator Identity
   role check in the service; client role claims cannot select this privilege.
2. A target organization/resource in the route and a nonblank reason of 10-500
   characters. This is not a session-wide impersonation or elevation switch.
3. The signed-in client's fresh CSRF token. The sensitive request quota also applies.
4. An organization-local database transaction. The explicit target applies only
   for this operation and the previous scoped context is restored in `finally`.
5. A committed audit record **before** returning the DTO. Failed audit persistence
   fails the operation; it never returns the resource successfully.

AuditLog reuses the existing table with optional `PlatformActorUserId` (global
User FK) and `Reason`. The existing member-scoped `ActorUserId` FK is unchanged;
a CHECK disallows simultaneous member and platform actors. Stored fields include
target organization, actor, resource type/ID, action, reason, UTC timestamps and
the request trace ID. Valid organizations with missing/wrong-organization
resource IDs generate `PlatformResourceReadNotFound` audit records and a 404.
Invalid reasons and unauthenticated/nonadmin requests are rejected before access
and remain in correlated request logs; they are not successful-access audit records.

Read-only platform access can target inactive/archived organizations for support;
the super administrator account itself must still be active and email-confirmed.
No global data listing, arbitrary SQL, bulk export, platform writes or public
administrator provisioning is introduced.

## PostgreSQL RLS

`AddResourceAuthorization` adds both the schema relationships and PostgreSQL RLS.
RLS is enabled and **forced** on:

```text
properties, property_manager_assignments, units, tenants, rental_applications,
leases, payments, expenses, maintenance_requests, contractors, documents,
notifications, audit_logs
```

Every policy has both USING (visibility/deletion) and WITH CHECK (insert/update).
The `public.demeure_can_access_organization(uuid)` SQL function is stable,
SECURITY INVOKER with a fixed search path; it does not run as an elevated owner.
It requires:

- The row organization matches transaction-local `demeure.organization_id`.
- `demeure.user_id` identifies an active, confirmed local account.
- Ordinary context has a live active membership in the active target organization.
- Explicit `demeure.platform_access=true` instead requires the stored global
  SuperAdministrator role; it still restricts rows to the explicit target organization.

Resource reads and platform audits use `OrganizationDataTransaction`, which starts
a transaction and sets all three values using parameterized `set_config(..., true)`.
Values disappear on commit/rollback, including exceptions; no connection-global
settings or pooled-connection identity state are used. Missing context denies rows,
and WITH CHECK denies writes. New use cases/background jobs must use the same
transaction boundary; unscoped business queries are not given a silent fallback.
Do not use connection/session-level `set_config(..., false)` or caller-supplied
SQL/context. Do not run parallel operations on one scoped DbContext.

### Bootstrap boundary and limitations

Organizations, organization memberships, global Users and standard Identity tables
are intentionally outside RLS: login, registration and membership discovery run
before there is a selected organization. They have no generic public read endpoints.
Authentication's cross-organization lookups remain narrow and tied to the current
authenticated user; normal EF Organization/Membership filters and tracked write
guards remain intact. This avoids a privileged SECURITY DEFINER bootstrap bypass.

RLS is a defense-in-depth **organization boundary**, not a replacement for the
manager/tenant/contractor predicates. Database credentials and custom context
settings belong to the trusted application. RLS does not protect against a
compromised runtime that can deliberately forge another user's settings or write
global Identity/membership tables. Do not give SQL execution or database credentials
to end users, and keep all SQL parameterized.

Use a **non-superuser, non-BYPASSRLS, non-schema-owner runtime login**, distinct
from the migration owner, with only required DML/sequence/function/schema usage
privileges. Do not grant CREATE, table ownership, role administration, TRUNCATE or
membership in elevated database roles. FORCE RLS also covers normal table owners,
but owners can alter the policy and are therefore not suitable runtime principals.
PostgreSQL superusers/BYPASSRLS roles bypass RLS; using them for the API invalidates
the database isolation guarantee. Migration credentials stay outside runtime
configuration and source control.

## Migrations and verification

Apply the selected provider migration explicitly; do not switch migration histories:

```powershell
dotnet ef database update --project backend\Demeure.Infrastructure --context SqliteDemeureDbContext
dotnet ef migrations has-pending-model-changes --project backend\Demeure.Infrastructure --context SqliteDemeureDbContext
dotnet ef migrations has-pending-model-changes --project backend\Demeure.Infrastructure --context DemeureDbContext
```

Use the [database configuration instructions](database.md) to set explicit local
SQLite paths or PostgreSQL migration connections. Back up existing databases and
exclude concurrent writers; SQLite rebuilds affected tables for added FKs/CHECKs.
Existing unassigned managers and unlinked tenants/contractors get no implicit
access. Assignment provisioning remains a separately requested workflow.

HTTP integration tests exercise real Identity cookies, all seven read endpoints,
same-organization unassigned records, foreign organization IDs, forged headers,
multiple memberships, live revocation, CSRF and explicit platform auditing.
Relational tests enforce assignment FKs/uniqueness; PostgreSQL SQL-generation tests
check every RLS table, policy, role predicate and rollback SQL.

An opt-in live PostgreSQL test creates a uniquely named disposable database and
NOLOGIN/NOBYPASSRLS role on a **dedicated test server**, migrates and seeds test-only
records with an admin connection, then SET ROLE tests RLS even as the table owner.
It verifies missing/forged context, identifier tampering, WITH CHECK, explicit
platform access and transaction reset, and removes only its named database/role.
Set `DEMEURE_TEST_POSTGRES_ADMIN` through your local environment/secret manager
to that isolated server's superuser connection, then run:

```powershell
dotnet test backend\Demeure.Tests\Demeure.Tests.csproj --filter "FullyQualifiedName~PostgreSqlRlsTests"
```

**No PostgreSQL server is currently available**, so this test is explicitly skipped.
Generated SQL/model checks are not evidence of live PostgreSQL RLS execution.
Running this live test plus deployment-role checks is mandatory before production.

## Files introduced

- [PropertyManagerAssignment.cs](../backend/Demeure.Domain/Modules/Properties/Entities/PropertyManagerAssignment.cs)
- [ResourceContracts.cs](../backend/Demeure.Application/Common/Authorization/ResourceContracts.cs)
- [ResourceReader.cs](../backend/Demeure.Infrastructure/Authorization/ResourceReader.cs)
- [OrganizationDataTransaction.cs](../backend/Demeure.Infrastructure/Authorization/OrganizationDataTransaction.cs)
- [PostgreSqlOrganizationSecurity.cs](../backend/Demeure.Infrastructure/Persistence/PostgreSqlOrganizationSecurity.cs)
- [ResourceEndpoints.cs](../backend/Demeure.Api/Endpoints/ResourceEndpoints.cs)
- [OrganizationAuthorizationTests.cs](../backend/Demeure.Tests/Integration/OrganizationAuthorizationTests.cs)
- [PostgreSqlRlsTests.cs](../backend/Demeure.Tests/Integration/PostgreSqlRlsTests.cs)
- [PropertyManagerAssignmentTests.cs](../backend/Demeure.Tests/Unit/PropertyManagerAssignmentTests.cs)
- PostgreSQL [migration](../backend/Demeure.Infrastructure/Persistence/Migrations/PostgreSql/20261105184128_AddResourceAuthorization.cs)
  and [designer](../backend/Demeure.Infrastructure/Persistence/Migrations/PostgreSql/20261105184128_AddResourceAuthorization.Designer.cs).
- SQLite [migration](../backend/Demeure.Infrastructure/Persistence/Migrations/Sqlite/20261105184131_AddResourceAuthorization.cs)
  and [designer](../backend/Demeure.Infrastructure/Persistence/Migrations/Sqlite/20261105184131_AddResourceAuthorization.Designer.cs).
- This guide.

## Files modified

- Domain: [Contractor.cs](../backend/Demeure.Domain/Modules/Contractors/Entities/Contractor.cs),
  [AuditLog.cs](../backend/Demeure.Domain/Common/AuditLog.cs).
- Persistence: [DemeureDbContext.cs](../backend/Demeure.Infrastructure/Persistence/DemeureDbContext.cs),
  [DemeureModelConfiguration.cs](../backend/Demeure.Infrastructure/Persistence/DemeureModelConfiguration.cs),
  [PostgreSQL snapshot](../backend/Demeure.Infrastructure/Persistence/Migrations/PostgreSql/DemeureDbContextModelSnapshot.cs),
  [SQLite snapshot](../backend/Demeure.Infrastructure/Persistence/Migrations/Sqlite/SqliteDemeureDbContextModelSnapshot.cs).
- Composition/security: [authentication DI](../backend/Demeure.Infrastructure/Authentication/DependencyInjection.cs),
  [AuthenticationConfiguration.cs](../backend/Demeure.Api/Extensions/AuthenticationConfiguration.cs),
  [Program.cs](../backend/Demeure.Api/Program.cs), [Demeure.Api.http](../backend/Demeure.Api/Demeure.Api.http).
- Tests: [AuthenticationTests.cs](../backend/Demeure.Tests/Integration/AuthenticationTests.cs),
  [PersistenceTests.cs](../backend/Demeure.Tests/Integration/PersistenceTests.cs),
  [PostgreSqlModelTests.cs](../backend/Demeure.Tests/Unit/PostgreSqlModelTests.cs).
- Documentation/environment: [README.md](../README.md), [architecture.md](architecture.md),
  [database.md](database.md), [authentication.md](authentication.md), [domain.md](domain.md),
  [backend environment reference](../backend/.env.example).
